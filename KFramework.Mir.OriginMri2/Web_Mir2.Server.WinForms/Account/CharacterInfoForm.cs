using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Server.Account;
using Server.Database;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirForms.Systems;
using Server.MirObjects;
using Server.Systems;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Server.Account
{
    public partial class CharacterInfoForm : Form
    {
        public CharacterInfoForm()
        {
            InitializeComponent();
            LoadCharacters();
        }

        #region Load Characters
        private void LoadCharacters()
        {
            CharacterCountLabel.Text = string.Format("Characters count: {0}", SMain.Envir.CharacterList.Count);

            CharactersList.Items.Clear();

            var characterList = SMain.Envir.CharacterList;

            if (characterList == null)
            {
                MessageBox.Show("Character list is not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            foreach (var character in characterList)
            {
                string characterIndex = character.Index.ToString();
                string characterPlayer = character.Name;
                string accountName = character.AccountInfo.AccountID;
                string accountIP = character.AccountInfo.CreationIP;

                ListViewItem item = new ListViewItem(characterIndex);
                item.SubItems.Add(characterPlayer);
                item.SubItems.Add(accountName);
                item.SubItems.Add(accountIP);
                item.Tag = character;

                CharactersList.Items.Add(item);
            }
        }
        #endregion

        #region Filter
        private List<ListViewItem> originalItems = new List<ListViewItem>();

        // 双击角色条目 → 打开玩家基础信息编辑界面（在线/离线角色都支持）
        private void CharactersList_DoubleClick(object sender, EventArgs e)
        {
            if (CharactersList.SelectedItems.Count == 0) return;
            CharacterInfo character = CharactersList.SelectedItems[0].Tag as CharacterInfo;
            if (character == null) return;

            using (var form = new PlayerInfoForm(character))
                form.ShowDialog(this);
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RefreshInterface();
        }
        public void RefreshInterface()
        {
            if (InvokeRequired)
            {
                Invoke((Action)RefreshInterface);
                return;
            }

            // Update Character count label
            CharacterCountLabel.Text = string.Format("Characters count: {0}", SMain.Envir.CharacterList.Count);

            // Get filtered characters based on filter text and checkbox state
            List<CharacterInfo> filteredCharacters = SMain.Envir.CharacterList;

            if (FilterPlayerTextBox.Text.Length > 0)
            {
                filteredCharacters = SMain.Envir.MatchPlayer(FilterPlayerTextBox.Text, MatchFilterCheckBox.Checked);
            }

            if (FilterItemTextBox.Text.Length > 0)
            {
                filteredCharacters = SMain.Envir.MatchPlayerbyItem(FilterItemTextBox.Text, MatchFilterCheckBox.Checked);
            }

            // Clear existing items in CharactersList
            CharactersList.Items.Clear();

            // Populate CharactersList with filtered character information
            foreach (CharacterInfo character in filteredCharacters)
            {
                ListViewItem tempItem = CreateListViewItem(character);

                if (tempItem != null)
                {
                    CharactersList.Items.Add(tempItem);
                }
            }
        }


        private ListViewItem CreateListViewItem(CharacterInfo character)
        {
            ListViewItem item = new ListViewItem(character.Index.ToString());
            item.SubItems.Add(character.Name);
            item.SubItems.Add(character.AccountInfo.AccountID);
            item.SubItems.Add(character.AccountInfo.CreationIP);

            item.Tag = character; // Store CharacterInfo object in Tag for reference

            return item;
        }

        #endregion
    }
}