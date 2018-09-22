using com.clusterrr.hakchi_gui.ModHub.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.ModHub
{
    public partial class ModHubForm : Form
    {
        List<Hmod.Hmod> installedMods;
        public ModHubForm()
        {
            InitializeComponent();
            installedMods = Hmod.Hmod.GetMods();
        }
        public void LoadData(IEnumerable<Repository.Repository.Item> repoItems)
        {
            var categories = new List<string>();
            foreach (var mod in repoItems)
            {
                if (!categories.Contains(mod.Category))
                {
                    categories.Add(mod.Category);
                }
            }

            categories.Sort();

            foreach (var category in categories)
            {
                var items = repoItems.Where((o) => o.Kind != Repository.Repository.ItemKind.Game && o.Category != null && o.Category.Equals(category, System.StringComparison.CurrentCultureIgnoreCase));
                if (items.Count() == 0)
                    continue;

                var page = new TabPage(category);
                var tabControl = new ModHubTabControl();
                tabControl.SetInstallButtonState(true);
                tabControl.parentForm = this;
                tabControl.installedMods = installedMods;
                tabControl.LoadData(items);
                tabControl.Dock = DockStyle.Fill;
                page.Controls.Add(tabControl);
                tabControl1.TabPages.Add(page);
            }
        }
    }
}
