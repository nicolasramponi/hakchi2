using com.clusterrr.hakchi_gui.Properties;
using com.clusterrr.hakchi_gui.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static com.clusterrr.hakchi_gui.Tasks.Tasker;

namespace com.clusterrr.hakchi_gui.ModHub.Controls
{
    public partial class ModHubTabControl : UserControl
    {
        public ModHubForm parentForm = null;
        public List<Hmod.Hmod> installedMods = new List<Hmod.Hmod>();
        public ModHubTabControl()
        {
            InitializeComponent();
        }
        public void SetInstallButtonState(bool visible)
        {
            modDownloadInstallButton.Visible = visible;
        }
        public void LoadData(IEnumerable<Repository.Repository.Item> items)
        {
            var groupEmulated = false;
            modList.Items.Clear();
            foreach (var mod in items)
            {
                var listItem = new ListViewItem(mod.Name);
                listItem.Tag = mod;
                modList.Items.Add(listItem);
                groupEmulated = groupEmulated || mod.EmulatedSystem != null;
            }
            if (groupEmulated)
                GroupList("Emulated System");
            SelectFirstItem();
        }

        public void SelectFirstItem()
        {
            if(modList.Items.Count > 0)
            {
                foreach (ListViewItem selectedItem in modList.SelectedItems)
                    selectedItem.Selected = false;

                modList.Items[0].Selected = true;
            }
        }

        public void GroupList(string key)
        {
            modList.Groups.Clear();
            var listGroups = new SortedDictionary<string, ListViewGroup>();
            foreach(ListViewItem item in modList.Items)
            {
                modList.Items.Remove(item);
                var mod = (Repository.Repository.Item)(item.Tag);
                var groupName = mod.Readme.frontMatter.ContainsKey(key) ? mod.Readme.frontMatter[key] : "​Unknown"; // Unknown contains a zero width space
                ListViewGroup group;
                if (!listGroups.TryGetValue(groupName.ToLower(), out group))
                {
                    group = new ListViewGroup(groupName, HorizontalAlignment.Center);
                    listGroups.Add(groupName.ToLower(), group);
                }
                item.Group = group;
                modList.Items.Add(item);
            }

            foreach(var group in listGroups.Values)
            {
                modList.Groups.Add(group);
            }
        }

        private void modList_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            modDownloadButton.Enabled = (modList.SelectedItems.Count > 0);
            modDownloadInstallButton.Enabled = (modList.SelectedItems.Count > 0);

            if (modList.SelectedItems.Count > 0)
            {
                var mod = (Repository.Repository.Item)(modList.SelectedItems[0].Tag);
                var installedMod = installedMods.Where((o) => o.RawName == mod.RawName);
                
                modInfo.SetInfo(mod.Name, mod.Creator, mod.Version, installedMod.Count() > 0 ? installedMod.First().Version ?? Resources.Unknown : null);
                modReadme.setReadme(mod.Name, mod.Readme);
            }
            else
            {
                modInfo.SetInfo(null, null, null, null);
                modReadme.clear();
            }
        }

        private IEnumerable<TaskFunc> GetItemDownloadTask(Repository.Repository.Item item)
        {
            var taskFuncs = new List<TaskFunc>();
            var modPath = Path.Combine(Program.BaseDirectoryExternal, "user_mods", item.FileName);

            switch (item.Kind)
            {
                case Repository.Repository.ItemKind.Hmod:
                    if (File.Exists(modPath))
                        File.Delete(modPath);

                    if (Directory.Exists(modPath))
                        Directory.Delete(modPath, true);

                    taskFuncs.Add(WebClientTasks.DownloadFile(item.URL, modPath));

                    break;

                case Repository.Repository.ItemKind.Game:
                    break;
            }
            return taskFuncs;
        }

        private void handleModDownload(bool install = false)
        {
            using (var tasker = new Tasks.Tasker(parentForm))
            {
                tasker.AttachViews(new TaskerTaskbar(), new TaskerForm());
                tasker.SetStatusImage(Resources.sign_cogs);
                tasker.SetTitle("Processing Mods");

                List<string> selectedMods = new List<string>();
                foreach (ListViewItem item in modList.SelectedItems)
                {
                    var mod = (Repository.Repository.Item)(item.Tag);
                    tasker.AddTasks(GetItemDownloadTask(mod));

                    if(mod.Kind == Repository.Repository.ItemKind.Hmod)
                        selectedMods.Add(mod.RawName);
                }

                if (install && selectedMods.Count > 0)
                    tasker.AddTasks(new MembootTasks(MembootTasks.MembootTaskType.ProcessMods, selectedMods.ToArray()).Tasks);

                tasker.Start();

                foreach(var mod in selectedMods)
                {
                    var found = installedMods.Where((o) => o.RawName.Equals(mod, System.StringComparison.CurrentCultureIgnoreCase)).ToArray();
                    foreach(var foundMod in found)
                    {
                        installedMods.Remove(foundMod);
                    }
                }
                
                installedMods.AddRange(Hmod.Hmod.GetMods(true, selectedMods.ToArray(), parentForm));
            }
        }

        private void modDownloadButton_Click(object sender, System.EventArgs e)
        {
            handleModDownload();
        }

        private void modDownloadInstallButton_Click(object sender, System.EventArgs e)
        {
            handleModDownload(true);
        }
    }
}
