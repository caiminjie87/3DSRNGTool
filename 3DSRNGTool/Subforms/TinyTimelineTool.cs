﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Pk3DSRNGTool.Controls;

namespace Pk3DSRNGTool
{
    public partial class TinyTimelineTool : Form
    {
        #region Basic UI
        public static readonly int[] typelist = { -1, 0, 1, 3, 4, 6, };
        public static readonly string[] typestrlist = { "-", "Blink(+2)", "Blink(+1)", "Stretch", "Soaring", "Running NPC" };
        private static readonly string[] methodlist = { "Instant Sync", "Cutscenes Sync", "Horde", "Friend Safari", "Poke Radar", "Fishing", "Rock Smash", "Cave Shadow", "Normal Wild", };
        public TinyTimelineTool()
        {
            InitializeComponent();
            MainDGV.AutoGenerateColumns = false;
            UpdateTypeComboBox(typelist);
            Type3.SelectedIndex =
            Type2.SelectedIndex =
            Type1.SelectedIndex = 0;
            Frame1.Maximum = Frame2.Maximum = Frame3.Maximum = TargetFrame.Maximum = FuncUtil.MAXFRAME;
            Frame1.Value = Frame2.Value = Frame3.Value = 500;
        }
        public void Translate()
        {
            this.TranslateInterface();
            Method.Items.Clear();
            Method.Items.AddRange(methodlist.Select(s => StringItem.Translate(s)).ToArray());
            Method.SelectedIndex = 0;
        }
        public void UpdateTypeComboBox(int[] type)
        {
            var List = typelist.Select((t, i) => new ComboItem(typestrlist[i], t)).Where(t => type.Contains(t.Value));
            Type1.DisplayMember = "Text";
            Type1.ValueMember = "Value";
            Type1.DataSource = new BindingSource(List.Skip(1), null);
            Type2.DisplayMember = "Text";
            Type2.ValueMember = "Value";
            Type2.DataSource = new BindingSource(List, null);
            Type3.DisplayMember = "Text";
            Type3.ValueMember = "Value";
            Type3.DataSource = new BindingSource(List, null);
        }
        private void TinyTimelineTool_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            this.Parent = null;
            e.Cancel = true;
        }
        public uint[] Gen6Tiny
        {
            get => new[] { tiny0.Value, tiny1.Value, tiny2.Value, tiny3.Value };
            set
            {
                if (value.Length < 4) return;
                tiny0.Value = value[0]; tiny1.Value = value[1];
                tiny2.Value = value[2]; tiny3.Value = value[3];
            }
        }
        private bool IsORAS => Program.mainform.IsORAS && new[] { 2, 5, 6, 8 }.Contains(Method.SelectedIndex);
        public bool HasSeed => Gen6Tiny.Any(t => t != 0);
        private void Update_Click(object sender, EventArgs e)
        {
            if (!NTRHelper.ntrclient?.Connected ?? true)
            {
                Program.mainform.TryToConnectNTR(false);
                return;
            }
            if (B_Stop.Visible)
                return;
            NTRHelper.ntrclient.ReadTiny("TTT");
        }
        public List<int> SkipList = new List<int>();
        public void Calibrate(int type, int Curr, int Next)
        {
            if (SkipList.Count == TypeNum.Value || type < 0 || type > 6)    // All used
            {
                B_Stop_Click(null, null);
                return;
            }
            if (SkipList.IndexOf(Curr) > -1) // Skip
                SkipList[SkipList.IndexOf(Curr)] = Next;
            else
            {
                SkipList.Add(Next);
                ((NumericUpDown)Controls.Find("Frame" + SkipList.Count.ToString(), true).FirstOrDefault()).Value = Curr;
                try { ((ComboBox)Controls.Find("Type" + SkipList.Count.ToString(), true).FirstOrDefault()).SelectedValue = type; } catch { };
                B_Create_Click(null, null);
            }
            return;
        }

        private void B_Cali_Click(object sender, EventArgs e)
        {
            if (!NTRHelper.ntrclient?.DebuggerEnabled ?? true)
            {
                if (FormUtil.Prompt(MessageBoxButtons.RetryCancel, "Connection lost, please double check your setup:\n(1) Disable PSS communications.\n(2) Use One Click function.") == DialogResult.Retry)
                    Program.mainform.TryToConnectNTR(true);
                return;
            }
            NTRHelper.ntrclient.ReadTiny("TTT");
            B_Stop.Visible = true;
            B_Cali.Visible = false;
            SkipList.Clear();
            Type2.SelectedValue = Type3.SelectedValue = -1;
            NTRHelper.ntrclient.EnableBP((int)Type1.SelectedValue == 4);
        }
        private void Cry_EnabledChanged(object sender, EventArgs e)
        {
            Cry.Checked &= Cry.Enabled;
        }
        private void B_Stop_Click(object sender, EventArgs e)
        {
            B_Stop.Visible = false;
            B_Cali.Visible = true;
            NTRHelper.ntrclient.DisableBP();
            SkipList.Clear();
        }
        private void TypeNum_ValueChanged(object sender, EventArgs e)
        {
            for (int i = 1; i <= TypeNum.Maximum; i++)
            {
                Controls.Find("Frame" + i.ToString(), true).FirstOrDefault().Enabled = i <= TypeNum.Value;
                Controls.Find("Type" + i.ToString(), true).FirstOrDefault().Enabled = i <= TypeNum.Value;
            }
        }
        private void Type_EnabledChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox type && type.Enabled == false)
                type.SelectedValue = -1;
        }
        #endregion

        private List<Frame_Tiny> list = new List<Frame_Tiny>();

        private void B_Create_Click(object sender, EventArgs e)
        {
            list.Clear();
            list = new List<Frame_Tiny>();
            Frame_Tiny.Startingframe = (int)Frame1.Value;
            var state = gettimeline();
            state.Generate(splittimeline: ConsiderDelay.Checked);
            list = state.results;
            MainDGV.DataSource = list;
            MainDGV.CurrentCell = null;
            int target = (int)TargetFrame.Value;
            if (ConsiderDelay.Checked && list.Count > 0)
            {
                int targetframeindex = list.FindIndex(t => t.framemin < target && target <= t.framemax);
                if (targetframeindex > 0)
                {
                    MainDGV.FirstDisplayedScrollingRowIndex = Math.Max(0, targetframeindex - 5);
                    MainDGV.Rows[targetframeindex].Selected = true;
                }
            }
            Method_Changed();
        }

        public TinyTimeline gettimeline()
        {
            var line = new TinyTimeline(Gen6Tiny)
            {
                Startingframe = (int)Frame1.Value,
                Maxframe = (int)TargetFrame.Value + 5000,
                CryFrame = Cry.Checked ? (int)CryFrame.Value : -1,
                Delay = ConsiderDelay.Checked ? (int)Delay.Value : 0,
                Method = (byte)Method.SelectedIndex,
                Parameter = (int)Parameters.Value,
                IsORAS = IsORAS,
            };
            line.Add((int)Frame1.Value, (int)Type1.SelectedValue);
            if (Frame2.Value > Frame1.Value)
            {
                line.Add((int)Frame2.Value, (int)Type2.SelectedValue);
                if (Frame3.Value > Frame2.Value)
                    line.Add((int)Frame3.Value, (int)Type3.SelectedValue);
            }
            return line;
        }

        private void MainDGV_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            int index = e.RowIndex;
            if (list.Count <= index)
                return;
            var row = MainDGV.Rows[index];
            if (tiny_enctr.Visible && list[index].enctr < Frame_Tiny.thershold && Frame_Tiny.thershold < 50)
                row.DefaultCellStyle.BackColor = System.Drawing.Color.LightYellow;
        }

        private void SetAsCurrent_Click(object sender, EventArgs e)
        {
            try
            {
                var seed = (string)MainDGV.CurrentRow.Cells["dgv_state"].Value;
                Gen6Tiny = FuncUtil.SeedStr2Array(seed) ?? Gen6Tiny;
            }
            catch (NullReferenceException)
            {
            }
        }

        private void Method_Changed()
        {
            tiny_ha.Visible = Method.SelectedIndex == 2;
            tiny_enctr.Visible = Method.SelectedIndex == 3 || Method.SelectedIndex > 4;
            dgv_slot.Visible = Method.SelectedIndex > 1;
            tiny_flute.Visible = IsORAS;
            tiny_flute.Width = Method.SelectedIndex == 2 ? 60 : 40;
            dgv_item.Width = Method.SelectedIndex == 2 ? 125 : 40;
            dgv_item.Visible = Method.SelectedIndex > 1;
            tiny_rand100.Visible = !ConsiderDelay.Checked;
            tiny_hitidx.Visible = ConsiderDelay.Checked;
        }

        private void MainDGV_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hti = MainDGV.HitTest(e.X, e.Y);
                    MainDGV.ClearSelection();
                    MainDGV.CurrentCell = MainDGV.Rows[hti.RowIndex].Cells[hti.ColumnIndex];
                }
            }
            catch { };
        }

        private void Method_SelectedIndexChanged(object sender, EventArgs e)
        {
            Parameters.Visible = true;
            ConsiderDelay.Enabled = Delay.Enabled = true;
            Cry.Enabled = CryFrame.Enabled = false;
            Cry.Checked = false;
            CryFrame.Value = 0;
            TypeNum.Value = 1;
            TTTToolTip.RemoveAll();
            UpdateTypeComboBox(new[] { -1, 0, 1, 3 });
            switch (Method.SelectedIndex)
            {
                case 0: // Instant Sync
                    Cry.Enabled = CryFrame.Enabled = true;
                    Parameters.Visible = false;
                    break;
                case 1: // Portal/soaring
                    Parameters.Maximum = 6;
                    Parameters.Minimum = 1;
                    Parameters.Value = 6;
                    UpdateTypeComboBox(new[] { -1, 0, 1, 3, 4 });
                    TTTToolTip.SetToolTip(Parameters, "Number of Party Pokemon");
                    Cry.Enabled = CryFrame.Enabled = true;
                    break;
                case 2: // Horde
                    Parameters.Maximum = 6;
                    Parameters.Minimum = 1;
                    Parameters.Value = 6;
                    TTTToolTip.SetToolTip(Parameters, "Number of Party Pokemon");
                    ConsiderDelay.Enabled = Delay.Enabled = false;
                    ConsiderDelay.Checked = false;
                    Delay.Value = 0;
                    break;
                case 3: // FS
                    Parameters.Maximum = 3;
                    Parameters.Minimum = 2;
                    Parameters.Value = 3;
                    Delay.Value = 6;
                    TTTToolTip.SetToolTip(Parameters, "Number of Encounter Slots");
                    break;
                case 4: // Pokeradar
                    Parameters.Maximum = 40;
                    Parameters.Minimum = 0;
                    Parameters.Value = 40;
                    Delay.Value = 14;
                    TTTToolTip.SetToolTip(Parameters, "Chain Length");
                    break;
                case 5: // Fishing
                    Parameters.Maximum = 6;
                    Parameters.Minimum = 1;
                    Parameters.Value = 6;
                    UpdateTypeComboBox(new[] { -1, 0, 1 });
                    Delay.Enabled = false;
                    ConsiderDelay.Checked = true;
                    Delay.Value = 14;
                    TTTToolTip.SetToolTip(Parameters, "Number of Party Pokemon");
                    break;
                case 6: // Rock Smash
                    UpdateTypeComboBox(new[] { -1, 0, 1 });
                    Parameters.Visible = false;
                    ConsiderDelay.Checked = true;
                    break;
                case 7: // Cave Shadow
                    Parameters.Visible = false;
                    TypeNum.Value = 2;
                    Delay.Enabled = false;
                    Delay.Value = 78;
                    Cry.Checked = true;
                    CryFrame.Value = 32;
                    break;
                case 8: // Normal Wilds
                    Parameters.Maximum = 99;
                    Parameters.Minimum = 1;
                    Parameters.Value = 1;
                    UpdateTypeComboBox(new[] { -1, 0, 1, 3, 6 });
                    Delay.Value = 6;
                    TTTToolTip.SetToolTip(Parameters, "Encounter Rate");
                    break;
                default:
                    Parameters.Visible = false;
                    break;
            }
        }
    }
}
