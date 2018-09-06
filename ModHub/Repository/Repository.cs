using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace com.clusterrr.hakchi_gui.ModHub.Repository
{
    public delegate void RepositoryProgressHandler(long current, long max);
    public delegate void RepositoryLoadedHandler();

    public static class ItemKindMethods
    {
        public static string GetFileExtension(this Repository.ItemKind kind)
        {
            return Repository.ItemKindFileExtensions[(int)kind];
        }
    }

    public class Repository
    {
        public event RepositoryProgressHandler RepositoryProgress;
        public event RepositoryLoadedHandler RepositoryLoaded;

        public string RepositoryURL { get; private set; }
        public string RepositoryPackURL
        {
            get
            {
                return RepositoryURL + "pack.tgz";
            }
        }

        public string RepositoryListURL
        {
            get
            {
                return RepositoryURL + "list";
            }
        }

        public Dictionary<ItemKind, List<Item>> Items = new Dictionary<ItemKind, List<Item>>();

        public static string[] ItemKindFileExtensions = new string[] { null, ".hmod", ".clvg" };
        public enum ItemKind
        {
            Unknown,
            Hmod,
            Game
        }

        public static ItemKind ItemKindFromFilename(string filename)
        {
            string lowerFilename = filename.ToLower();

            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                if (kind == ItemKind.Unknown) continue;
                if (lowerFilename.EndsWith(kind.GetFileExtension()))
                    return kind;
            }

            return ItemKind.Unknown;
        }
        
        public class Item
        {
            public string FileName { get; private set; }
            public string Name { get; private set; }
            public string Category { get; private set; }
            public string Creator { get; private set; }
            public string Version { get; private set; }
            public string EmulatedSystem { get; private set; }
            public string URL { get; private set; }
            public string MD5 { get; private set; }
            public string SHA1 { get; private set; }
            public bool Extract { get; private set; }

            public ItemKind Kind { get; private set; }
            public HmodReadme Readme { get; private set; }

            public Item(string filename, string readme = null, bool markdownReadme = false)
            {
                FileName = filename;
                Kind = ItemKindFromFilename(FileName);
                Name = FileName;
                Category = null;
                Creator = null;
                Version = null;
                EmulatedSystem = null;
                URL = null;
                MD5 = null;
                SHA1 = null;
                Extract = false;
                Readme = new HmodReadme(readme ?? "", markdownReadme);
                setValues();
            }
            public void setURL(string url)
            {
                URL = url;
            }
            public void setMD5(string md5)
            {
                MD5 = md5;
            }
            public void setSHA1(string sha1)
            {
                SHA1 = sha1;
            }
            public void setExtract(bool extract)
            {
                Extract = extract;
            }
            public void setReadme(string readme, bool markdown = false)
            {
                Readme = new HmodReadme(readme, markdown);
                setValues();
            }
            private void setValues()
            {
                Name = Readme.frontMatter.ContainsKey("Name") ? Readme.frontMatter["Name"] : FileName;
                Category = Readme.frontMatter.ContainsKey("Category") ? Readme.frontMatter["Category"] : null;
                Creator = Readme.frontMatter.ContainsKey("Creator") ? Readme.frontMatter["Creator"] : null;
                Version = Readme.frontMatter.ContainsKey("Version") ? Readme.frontMatter["Version"] : null;
                EmulatedSystem = Readme.frontMatter.ContainsKey("Emulated System") ? Readme.frontMatter["Emulated System"] : null;
            }
        }

        public Repository(string repositoryURL)
        {
            this.RepositoryURL = repositoryURL + (repositoryURL.EndsWith("/") ? "" : "/");
            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                Items.Add(kind, new List<Item>());
            }
        }

        private string StreamToString(Stream stream)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            using (var sr = new StreamReader(stream, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        public async void Load()
        {
            using (WebClient wc = new WebClient())
            {
                var tempDict = new Dictionary<string, Item>();
                string[] list = new string[] { };
                MemoryStream repoPack = new MemoryStream(await wc.DownloadDataTaskAsync(new Uri(RepositoryPackURL)));
                using (var extractor = ArchiveFactory.Open(repoPack))
                {
                    var tar = new MemoryStream();
                    extractor.Entries.First().OpenEntryStream().CopyTo(tar);
                    tar.Seek(0, SeekOrigin.Begin);
                    using (var extractorTar = ArchiveFactory.Open(tar))
                    {
                        foreach (var entry in extractorTar.Entries)
                        {
                            if(Regex.Match(entry.Key, @"^(?:\./)?list").Success)
                            {
                                list = Regex.Replace(StreamToString(entry.OpenEntryStream()), @"[\r\n]+", "\n").Split("\n"[0]);
                                break;
                            }
                        }
                        foreach (var entry in extractorTar.Entries)
                        {
                            var match = Regex.Match(entry.Key, @"^(?:\./)?([^/]+)/(extract|link|md5|sha1|readme(?:\.(?:md|txt)?)?)$");
                            if (match.Success)
                            {
                                var mod = match.Groups[1].ToString();
                                var fileName = match.Groups[2].ToString();

                                if (!list.Contains(mod))
                                {
                                    continue;
                                }

                                Item item;

                                if (!tempDict.TryGetValue(mod, out item))
                                {
                                    item = new Item(mod);
                                    tempDict.Add(mod, item);
                                }

                                switch (fileName)
                                {
                                    case "extract":
                                        item.setExtract(true);
                                        break;

                                    case "link":
                                        item.setURL(StreamToString(entry.OpenEntryStream()).Trim());
                                        break;

                                    case "md5":
                                        item.setMD5(StreamToString(entry.OpenEntryStream()).Trim());
                                        break;

                                    case "sha1":
                                        item.setSHA1(StreamToString(entry.OpenEntryStream()).Trim());
                                        break;

                                    case "readme":
                                    case "readme.txt":
                                    case "readme.md":
                                        item.setReadme(StreamToString(entry.OpenEntryStream()).Trim(), fileName.EndsWith(".md"));
                                        break;
                                }

                            }
                        }
                    }
                }
                foreach (var key in tempDict.Keys.ToArray())
                {
                    var item = tempDict[key];
                    if (list.Contains(key))
                    {
                        Items[item.Kind].Add(item);
                    }
                    tempDict.Remove(key);
                }
                tempDict.Clear();
                tempDict = null;
                RepositoryLoaded();
                return;
            }

        }
    }
}
