﻿using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Hmod
{
    public partial class SelectForm : Form
    {
        private string hmodDisplayed = null;
        private List<Hmod> hmods = new List<Hmod>();
        private bool onlyInstalledMods;
        private string usermodsDirectory = Path.Combine(Program.BaseDirectoryExternal, "user_mods");

        public SelectForm(bool onlyInstalledMods, bool allowDropMods, string[] filesToAdd = null)
        {
            InitializeComponent();
            this.onlyInstalledMods = onlyInstalledMods;

            switch (ConfigIni.Instance.hmodListSort)
            {
                case ListSort.Category:
                    categoryToolStripMenuItem.Checked = true;
                    break;

                case ListSort.EmulatedSystem:
                    emulatedSystemToolStripMenuItem.Checked = true;
                    break;

                case ListSort.Creator:
                    creatorToolStripMenuItem.Checked = true;
                    break;
            }

            hmods.AddRange(Hmod.GetMods(hakchi.CanInteract && onlyInstalledMods, null, this));

            populateList();

            if (filesToAdd != null) AddMods(filesToAdd);
            this.AllowDrop = allowDropMods;
        }

        private void populateList()
        {
            SortedDictionary<string, ListViewGroup> listGroups = new SortedDictionary<string, ListViewGroup>();
            listViewHmods.BeginUpdate();
            listViewHmods.Groups.Clear();
            listViewHmods.Items.Clear();

            foreach (Hmod hmod in hmods)
            {
                string groupName = Properties.Resources.Unknown;

                switch (ConfigIni.Instance.hmodListSort)
                {
                    case ListSort.Category:
                        groupName = hmod.Category;
                        break;

                    case ListSort.EmulatedSystem:
                        groupName = hmod.EmulatedSystem;
                        break;

                    case ListSort.Creator:
                        groupName = hmod.Creator;
                        break;
                }

                ListViewGroup group;
                if (!listGroups.TryGetValue(groupName.ToLower(), out group))
                {
                    group = new ListViewGroup(groupName, HorizontalAlignment.Center);
                    listGroups.Add(groupName.ToLower(), group);
                }
                ListViewItem item = new ListViewItem(new String[] { hmod.Name, hmod.Creator });
                item.SubItems.Add(hmod.Creator);
                item.Tag = hmod;
                item.Group = group;
                item.ToolTipText = String.Join("\r\n", hmod.Readme.headingLines);

                if (!onlyInstalledMods && hmod.isInstalled)
                {
                    item.ForeColor = SystemColors.GrayText;
                }

                listViewHmods.Items.Add(item);
            }

            foreach (ListViewGroup group in listGroups.Values)
            {
                listViewHmods.Groups.Add(group);
            }

            listViewHmods.EndUpdate();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (listViewHmods.CheckedItems.Count > 0)
                DialogResult = DialogResult.OK;
            else
                DialogResult = DialogResult.Cancel;
            Close();
        }

        static string getHmodPath(string hmod)
        {
            try
            {
                Dictionary<string, string> readmeData = new Dictionary<string, string>();
                var dir = Shared.PathCombine(Program.BaseDirectoryExternal, "user_mods", hmod + ".hmod");
                if (Directory.Exists(dir))
                {
                    return dir + Path.DirectorySeparatorChar;
                }
                else if (File.Exists(dir))
                {
                    return dir;
                }
            }
            catch { }

            return null;
        }

        private void SelectModsForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void SelectModsForm_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddMods(files);
        }

        private void AddMods(string[] files)
        {
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                if (ext == ".hmod")
                {
                    var target = Path.Combine(usermodsDirectory, Path.GetFileName(file));
                    if (file != target)
                        if (Directory.Exists(file))
                            Shared.DirectoryCopy(file, target, true, false, true, false);
                        else
                            File.Copy(file, target, true);
                    hmods.Add(new Hmod(Path.GetFileNameWithoutExtension(file)));
                }
                else if (ext == ".7z" || ext == ".zip" || ext == ".rar")
                {
                    using (var extractor = ArchiveFactory.Open(file))
                    {
                        foreach (var f in extractor.Entries)
                        {
                            if (Path.GetExtension(f.Key).ToLower() == ".hmod")
                            {
                                if (f.IsDirectory)
                                {
                                    using (var reader = extractor.ExtractAllEntries())
                                    {
                                        while (reader.MoveToNextEntry())
                                        {
                                            if (!reader.Entry.IsDirectory && reader.Entry.Key.StartsWith(f.Key))
                                                reader.WriteEntryToDirectory(usermodsDirectory, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                        }
                                    }

                                    if (!Directory.Exists(Path.Combine(usermodsDirectory, Path.GetFileName(f.Key))))
                                    {
                                        Directory.Move(Path.Combine(usermodsDirectory, f.Key), Path.Combine(usermodsDirectory, Path.GetFileName(f.Key)));

                                        new DirectoryInfo(usermodsDirectory).Refresh();
                                        int pos = f.Key.IndexOfAny(new char[] { '/', '\\' });
                                        Directory.Delete(Path.Combine(usermodsDirectory, pos > 0 ? f.Key.Substring(0, pos) : f.Key), true);
                                    }

                                    hmods.Add(new Hmod(Path.GetFileNameWithoutExtension(f.Key)));
                                }
                                else
                                {
                                    using (var outFile = new FileStream(Path.Combine(usermodsDirectory, Path.GetFileName(f.Key)), FileMode.Create))
                                    {
                                        f.OpenEntryStream().CopyTo(outFile);
                                        hmods.Add(new Hmod(Path.GetFileNameWithoutExtension(f.Key)));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            populateList();
        }

        private void categoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            categoryToolStripMenuItem.Checked = true;
            creatorToolStripMenuItem.Checked = false;
            emulatedSystemToolStripMenuItem.Checked = false;

            ConfigIni.Instance.hmodListSort = ListSort.Category;
            populateList();
        }

        private void creatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            creatorToolStripMenuItem.Checked = true;
            categoryToolStripMenuItem.Checked = false;
            emulatedSystemToolStripMenuItem.Checked = false;

            ConfigIni.Instance.hmodListSort = ListSort.Creator;
            populateList();
        }

        private void listViewHmods_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.Item != null && listViewHmods.SelectedItems.Count > 0)
            {
                Hmod hmod = (Hmod)(listViewHmods.SelectedItems[0].Tag);
                if(hmodDisplayed != hmod.RawName)
                    readmeControl1.setReadme(hmod.Name, hmod.Readme);
            }
            else
            {
                readmeControl1.clear();
            }
        }

        private void SelectModsForm_Shown(object sender, EventArgs e)
        {
            splitContainer1_SplitterMoved(null, null);
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            listViewHmods.BeginUpdate();
            hmodName.Width = -1;
            hmodName.Width = listViewHmods.Width - 4 - SystemInformation.VerticalScrollBarWidth;
            listViewHmods.EndUpdate();
        }

        private void showModInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(listViewHmods.SelectedItems.Count > 0)
            {
                Hmod selectedHmod = (Hmod)(listViewHmods.SelectedItems[0].Tag);
                if (selectedHmod.PresentInUserMods())
                {
                    string args = $"/select, \"{selectedHmod.HmodPath}\"";
                    Process.Start("explorer.exe", args);
                }
            }
        }

        private void deleteModFromDiskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem selectedItem in listViewHmods.SelectedItems)
            {
                Hmod selectedHmod = (Hmod)(selectedItem.Tag);
                if (selectedHmod.PresentInUserMods())
                {
                    if (selectedHmod.isFile)
                    {
                        File.Delete(selectedHmod.HmodPath);
                    }
                    else
                    {
                        Directory.Delete(selectedHmod.HmodPath, true);
                    }
                    hmods.Remove(selectedHmod);
                }
            }
            populateList();
        }

        private void modListMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool isHmodSelected = (listViewHmods.SelectedItems.Count > 0);
            
            deleteModFromDiskToolStripMenuItem.Visible = !onlyInstalledMods && isHmodSelected;
            showModInExplorerToolStripMenuItem.Visible = (listViewHmods.SelectedItems.Count == 1 && ((Hmod)(listViewHmods.SelectedItems[0].Tag)).PresentInUserMods());
        }

        private void emulatedSystemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            emulatedSystemToolStripMenuItem.Checked = true;
            creatorToolStripMenuItem.Checked = false;
            categoryToolStripMenuItem.Checked = false;

            ConfigIni.Instance.hmodListSort = ListSort.EmulatedSystem;
            populateList();
        }
    }
}
