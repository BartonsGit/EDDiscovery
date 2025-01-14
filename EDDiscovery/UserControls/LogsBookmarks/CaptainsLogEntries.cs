﻿/*
 * Copyright © 2018-2022 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */

using EliteDangerousCore;
using EliteDangerousCore.DB;
using EliteDangerousCore.EDSM;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    // CL UCs use the UCCB template BUT are not directly inserted into the normal panels.. they are inserted into the CL UCCB
    // Make sure DB saving has unique names.. they all share the same displayno.

    public partial class CaptainsLogEntries : UserControlCommonBase
    {
        private string dbStartDate = "SD";
        private string dbStartDateOn = "SDOn";
        private string dbEndDate = "ED";
        private string dbEndDateOn = "EDOn";
        const string dbTags = "TagFilter";

        const int TagSize = 24;

        Timer searchtimer;
        bool updateprogramatically;

        #region init
        public CaptainsLogEntries()
        {
            InitializeComponent();
        }

        public override void Init()
        {
            DBBaseName = "CaptainsLogPanel";

            searchtimer = new Timer() { Interval = 500 };
            searchtimer.Tick += Searchtimer_Tick;
            GlobalCaptainsLogList.Instance.OnLogEntryChanged += LogChanged;

            var enumlist = new Enum[] { EDTx.CaptainsLogEntries_ColTime, EDTx.CaptainsLogEntries_ColSystem, EDTx.CaptainsLogEntries_ColBodyName, EDTx.CaptainsLogEntries_ColNote, EDTx.CaptainsLogEntries_ColTags, EDTx.CaptainsLogEntries_labelDateStart, EDTx.CaptainsLogEntries_labelEndDate, EDTx.CaptainsLogEntries_labelSearch };
            var enumlistcms = new Enum[] { EDTx.CaptainsLogEntries_toolStripMenuItemGotoStar3dmap, EDTx.CaptainsLogEntries_openInEDSMToolStripMenuItem, EDTx.CaptainsLogEntries_openAScanPanelViewToolStripMenuItem };
            var enumlisttt = new Enum[] { EDTx.CaptainsLogEntries_textBoxFilter_ToolTip, EDTx.CaptainsLogEntries_buttonNew_ToolTip, EDTx.CaptainsLogEntries_buttonDelete_ToolTip, EDTx.CaptainsLogEntries_buttonTags_ToolTip };

            BaseUtils.Translator.Instance.TranslateControls(this, enumlist, new Control[] { });
            BaseUtils.Translator.Instance.TranslateToolstrip(contextMenuStrip, enumlistcms, this);
            BaseUtils.Translator.Instance.TranslateTooltip(toolTip, enumlisttt, this);

            ColNote.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;

            dateTimePickerStartDate.Value = GetSetting(dbStartDate, EliteDangerousCore.EliteReleaseDates.GameRelease).StartOfDay();
            dateTimePickerStartDate.Checked = GetSetting(dbStartDateOn, false);
            dateTimePickerEndDate.Value = GetSetting(dbEndDate, DateTime.UtcNow).EndOfDay();
            dateTimePickerEndDate.Checked = GetSetting(dbEndDateOn, false);
            VerifyDates();
            dateTimePickerStartDate.ValueChanged += (s, e) => { if (!updateprogramatically) Display(); };
            dateTimePickerEndDate.ValueChanged += (s, e) => { if (!updateprogramatically) Display(); };

            DiscoveryForm.OnHistoryChange += Discoveryform_OnHistoryChange;

        }

        public override void LoadLayout()
        {
            DGVLoadColumnLayout(dataGridView);
        }

        public override void Closing()
        {
            DGVSaveColumnLayout(dataGridView);

            PutSetting(dbStartDate, dateTimePickerStartDate.Value);
            PutSetting(dbEndDate, dateTimePickerEndDate.Value);
            PutSetting(dbStartDateOn, dateTimePickerStartDate.Checked);
            PutSetting(dbEndDateOn, dateTimePickerEndDate.Checked);

            searchtimer.Dispose();
            GlobalCaptainsLogList.Instance.OnLogEntryChanged -= LogChanged;

            DiscoveryForm.OnHistoryChange -= Discoveryform_OnHistoryChange;
        }
        #endregion

        #region display

        public override void InitialDisplay()
        {
            Display();
        }

        private void Discoveryform_OnHistoryChange()
        {
            VerifyDates();      // if date time mode changes, history change is fired by settings. check date validation
            Display();
        }
        private void Display()
        {
            int lastrow = dataGridView.CurrentCell != null ? dataGridView.CurrentCell.RowIndex : -1;

            DataGridViewColumn sortcol = dataGridView.SortedColumn != null ? dataGridView.SortedColumn : dataGridView.Columns[0];
            SortOrder sortorder = dataGridView.SortOrder;

            dataViewScrollerPanel.SuspendLayout();
            dataGridView.SuspendLayout();

            dataGridView.Rows.Clear();

            // be paranoid about end/start of day
            DateTime startutc = dateTimePickerStartDate.Checked ? EDDConfig.Instance.ConvertTimeToUTCFromPicker(dateTimePickerStartDate.Value.StartOfDay()) : EliteDangerousCore.EliteReleaseDates.GameRelease;
            DateTime endutc = dateTimePickerEndDate.Checked ? EDDConfig.Instance.ConvertTimeToUTCFromPicker(dateTimePickerEndDate.Value.EndOfDay()) : EliteDangerousCore.EliteReleaseDates.GameEndTime;
            System.Diagnostics.Debug.WriteLine($"Captains Log display filter {startutc} {endutc}");

            foreach (CaptainsLogClass entry in GlobalCaptainsLogList.Instance.LogEntries)
            {
                if (entry.Commander == EDCommander.CurrentCmdrID)
                {
                    if ( entry.TimeUTC >= startutc && entry.TimeUTC <= endutc)
                    {
                        var rw = dataGridView.RowTemplate.Clone() as DataGridViewRow;
                        rw.CreateCells(dataGridView,
                            EDDConfig.Instance.ConvertTimeToSelectedFromUTC(entry.TimeUTC),
                            entry.SystemName,
                            entry.BodyName,
                            entry.Note,
                            ""
                            );

                        rw.Tag = entry;
                        rw.Cells[ColTime.Index].Tag = entry.TimeUTC;      // column 0 gets time utc
                        string tags = entry.Tags ?? "";

                        rw.Cells[ColTags.Index].Tag = tags;
                        var taglist = EDDConfig.CaptainsLogTagArray(tags);
                        rw.Cells[ColTags.Index].ToolTipText = string.Join(Environment.NewLine, taglist);
                        TagsForm.SetMinHeight(taglist.Length, rw, ColTags.Width, TagSize);

                        dataGridView.Rows.Add(rw);
                    }
                }
            }

            dataGridView.ResumeLayout();
            dataViewScrollerPanel.ResumeLayout();

            dataGridView.Sort(sortcol, (sortorder == SortOrder.Descending) ? System.ComponentModel.ListSortDirection.Descending : System.ComponentModel.ListSortDirection.Ascending);
            dataGridView.Columns[sortcol.Index].HeaderCell.SortGlyphDirection = sortorder;

            string tagfilter = GetSetting(dbTags, "All");
            if (textBoxFilter.Text.HasChars() || tagfilter != ExtendedControls.CheckedIconUserControl.All)
                FilterView();

            if (lastrow >= 0 && lastrow < dataGridView.Rows.Count && dataGridView.Rows[lastrow].Visible)
                dataGridView.SetCurrentAndSelectAllCellsOnRow(Math.Min(lastrow, dataGridView.Rows.Count - 1));
        }

        private void dataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewRow rw = dataGridView.Rows[e.RowIndex];
            var taglist = EDDConfig.CaptainsLogTagArray(rw.Cells[ColTags.Index].Tag as string);
            TagsForm.PaintTags(taglist, EDDConfig.Instance.CaptainsLogTagDictionary, 
                                dataGridView.GetCellDisplayRectangle(ColTags.Index, rw.Index, false), e.Graphics, TagSize);
        }

        private void dataGridView_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"Column resize {e.Column.Name}");
            if (e.Column == ColTags)
            {
                foreach (DataGridViewRow rw in dataGridView.Rows)
                {
                    var taglist = EDDConfig.CaptainsLogTagArray(rw.Cells[ColTags.Index].Tag as string);
                    TagsForm.SetMinHeight(taglist.Length, rw, ColTags.Width, TagSize);
                }
            }
        }

        private void dataGridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index == 0)
                e.SortDataGridViewColumnAlpha(true);
            else if (e.Column.Index == 4)
                e.SortDataGridViewColumnTagsAsStringsLists(4);
        }

        #endregion

        #region Button UI
        private void buttonNew_Click(object nu1, EventArgs nu2)
        {
            updateprogramatically = true;
            dateTimePickerStartDate.Checked = dateTimePickerEndDate.Checked = false;
            Display();
            updateprogramatically = false;

            HistoryEntry he = DiscoveryForm.History.GetLast;
            MakeNew(EDDConfig.Instance.ConvertTimeToSelectedFromUTC(DateTime.UtcNow), he?.System.Name ?? "?", he?.WhereAmI ?? "?");
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            int[] rows = null;

            if (dataGridView.SelectedCells.Count > 0)      // being paranoid
            {
                rows = (from DataGridViewCell x in dataGridView.SelectedCells select x.RowIndex).Distinct().ToArray();
            }

            //System.Diagnostics.Debug.WriteLine("cells {0} rows {1} selrows {2}", dataGridViewBookMarks.SelectedCells.Count, dataGridViewBookMarks.SelectedRows.Count , rows.Length);

            if (rows != null && rows.Length > 1)
            {
                if (ExtendedControls.MessageBoxTheme.Show(FindForm(), string.Format(("Do you really want to delete {0} notes?" + Environment.NewLine + "Confirm or Cancel").T(EDTx.CaptainsLogEntries_CFN), rows.Length), "Warning".T(EDTx.Warning), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    foreach (int r in rows)
                    {
                        CaptainsLogClass entry = (CaptainsLogClass)dataGridView.Rows[r].Tag;

                        if (entry != null)
                            GlobalCaptainsLogList.Instance.Delete(entry);
                    }
                    Display();
                }

            }
            else if (dataGridView.CurrentCell != null)      // if we have a current cell.. 
            {
                DataGridViewRow rw = dataGridView.CurrentCell.OwningRow;

                if (rw.Tag != null)
                {
                    CaptainsLogClass entry = (CaptainsLogClass)rw.Tag;

                    if (ExtendedControls.MessageBoxTheme.Show(FindForm(), string.Format(("Do you really want to delete the note for {0}" + Environment.NewLine + "Confirm or Cancel").T(EDTx.CaptainsLogEntries_CF), entry.SystemName + ":" + entry.BodyName), "Warning".T(EDTx.Warning), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        GlobalCaptainsLogList.Instance.Delete(entry);
                        Display();
                    }
                }
                else
                    Display();
            }
        }

        private void buttonTags_Click(object sender, EventArgs e)
        {
            TagsForm tg = new TagsForm();
            tg.Init("Set Tags".T(EDTx.CaptainsLogEntries_SetTags), this.FindForm().Icon, EDDConfig.TagSplitStringCL, EDDConfig.Instance.CaptainsLogTagDictionary);

            if (tg.ShowDialog() == DialogResult.OK)
            {
                EDDConfig.Instance.CaptainsLogTagDictionary = tg.Result;
                Display();
            }
        }

        #endregion


        #region Grid Editing

        // keydown on form, see if to edit
        private void dataGridView_KeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Cell key down" + e.KeyCode);

            if (e.KeyCode == Keys.Enter && dataGridView.CurrentCell != null )
            {
                DataGridViewRow rw = dataGridView.CurrentCell.OwningRow;

                if (dataGridView.CurrentCell.ColumnIndex == 3)
                {
                    EditNote(rw);
                    e.Handled = true;
                }
                else if (dataGridView.CurrentCell.ColumnIndex == 4)
                {
                    EditTags(rw);
                    e.Handled = true;
                }
            }
        }

        // click on item
        private void dataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)    // row -1 is the header..
            {
                DataGridViewRow rw = dataGridView.Rows[e.RowIndex];

                if (e.ColumnIndex == 3)
                    EditNote(rw);
                else if (e.ColumnIndex == 4)
                    EditTags(rw);
            }
        }

        private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            DataGridViewRow rw = dataGridView.Rows[e.RowIndex];

            if ( e.ColumnIndex == 0 )
            {
                // cell contains a datetime object (dec 22 bug) or maybe a string to be defensive

                string dt = rw.Cells[ColTime.Index].Value is DateTime ? ((DateTime)rw.Cells[ColTime.Index].Value).ToString() : rw.Cells[ColTime.Index].Value as string;

                System.Globalization.DateTimeStyles dts = System.Globalization.DateTimeStyles.AllowWhiteSpaces;     // convert straight no conversion

                if ( dt != null && DateTime.TryParse(dt, System.Globalization.CultureInfo.CurrentCulture, dts, out DateTime datetimeselected) && 
                            EDDConfig.Instance.DateTimeInRangeForGame(datetimeselected))
                {
                    var utc = EDDConfig.Instance.ConvertTimeToUTCFromPicker(datetimeselected);        // go to UTC like a picker
                    rw.Cells[ColTime.Index].Tag = utc;
                    System.Diagnostics.Debug.WriteLine($"Captains Log Edit row {rw.Index} datetime {datetimeselected} -> date time utc {rw.Cells[ColTime.Index].Tag}");
                    StoreRow(rw);
                }
                else
                {
                    ExtendedControls.MessageBoxTheme.Show(this.FindForm(), "Bad/Out of Range Date Time format".T(EDTx.CaptainsLogEntries_DTF), "Warning".T(EDTx.Warning), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DateTime prevutc = (DateTime)rw.Cells[ColTime.Index].Tag;
                    rw.Cells[ColTime.Index].Value = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(prevutc);       // note we go back to selected
                }
            }
            else 
            {
                StoreRow(rw);
            }
        }

        private void EditNote(DataGridViewRow rw)
        {
            string notes = rw.Cells[ColNote.Index].Value != null ? (string)rw.Cells[ColNote.Index].Value : "";

            string s = ExtendedControls.PromptSingleLine.ShowDialog(this.FindForm(), "Note:".T(EDTx.CaptainsLogEntries_Note), notes,
                            "Enter Note".T(EDTx.CaptainsLogEntries_EnterNote), this.FindForm().Icon, multiline: true, cursoratend: true, widthboxes:400, heightboxes:400);

            if (s != null)
            {
                rw.Cells[ColNote.Index].Value = s;
                StoreRow(rw);
            }
        }

        private void EditTags(DataGridViewRow rw)
        {
            string taglist = rw.Cells[ColTags.Index].Tag as string;
            TagsForm.EditTags(this.FindForm(), 
                                        EDDConfig.Instance.CaptainsLogTagDictionary, taglist, taglist,
                                        dataGridView.PointToScreen(dataGridView.GetCellDisplayRectangle(ColTags.Index, rw.Index, false).Location),
                                        TagsChanged, rw, EDDConfig.TagSplitStringCL);
        }

        private void TagsChanged(string newtags, Object tag)
        {
            DataGridViewRow rw = tag as DataGridViewRow;
            System.Diagnostics.Debug.Assert(rw.Index >= 0);
            rw.Cells[ColTags.Index].Tag = newtags;
            var taglist = EDDConfig.CaptainsLogTagArray(newtags);
            rw.Cells[ColTags.Index].ToolTipText = string.Join(Environment.NewLine, taglist);
            TagsForm.SetMinHeight(taglist.Length, rw, ColTags.Width, TagSize);
            StoreRow(rw);
            dataGridView.InvalidateRow(rw.Index);
        }

        private void StoreRow( DataGridViewRow rw)
        {
            inupdate = true;
            CaptainsLogClass entry = rw.Tag as CaptainsLogClass;        // may be null

            if ( rw.Cells[ColSystem.Index].IsNullOrEmpty())   // we must have system
                rw.Cells[ColSystem.Index].Value = "?";

            if (rw.Cells[ColBodyName.Index].IsNullOrEmpty())    // and body. User can remove these during editing.
                rw.Cells[ColBodyName.Index].Value = "?";

            string notes = rw.Cells[ColNote.Index].IsNullOrEmpty() ? "" : (string)rw.Cells[ColNote.Index].Value;
            string tags = rw.Cells[ColTags.Index].Tag as string;

            CaptainsLogClass cls = GlobalCaptainsLogList.Instance.AddOrUpdate(entry, EDCommander.CurrentCmdrID,
                           rw.Cells[ColSystem.Index].Value as string,
                           rw.Cells[ColBodyName.Index].Value as string,
                           (DateTime)rw.Cells[ColTime.Index].Tag,       // tag is UTC
                           notes,
                           tags);

            rw.Tag = cls;

            inupdate = false;
        }

        private void VerifyDates()
        {
            updateprogramatically = true;
            if (!EDDConfig.Instance.DateTimeInRangeForGame(dateTimePickerStartDate.Value) || !EDDConfig.Instance.DateTimeInRangeForGame(dateTimePickerEndDate.Value))
            {
                dateTimePickerStartDate.Checked = dateTimePickerEndDate.Checked = false;
                dateTimePickerStartDate.Value = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(EliteDangerousCore.EliteReleaseDates.GameRelease);
                dateTimePickerEndDate.Value = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(DateTime.UtcNow.EndOfDay());
            }
            updateprogramatically = false;
        }

        #endregion

        #region Interactions with other tabs

        public void SelectDate(DateTime datestartutc, DateTime dateendutc, bool createnew)
        {
            System.Diagnostics.Debug.WriteLine($"Selected date range {datestartutc}-{dateendutc}");

            updateprogramatically = true;

            dateTimePickerStartDate.Value = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(datestartutc);      // will assert if not utc
            dateTimePickerEndDate.Value = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(dateendutc);
            dateTimePickerEndDate.Checked = dateTimePickerStartDate.Checked = true;

            updateprogramatically = false;
            Display();

            if (createnew)
            {
                MakeNew(EDDConfig.Instance.ConvertTimeToSelectedFromUTC(datestartutc).StartOfDay(), "?", "?");
            }
        }

        private void MakeNew(DateTime selectedtime, string system, string body)
        {
            var rw = dataGridView.RowTemplate.Clone() as DataGridViewRow;

            rw.CreateCells(dataGridView,
                selectedtime,
                system,
                body,
                "",
                ""
             );

            rw.Tag = null;
            rw.Cells[ColTime.Index].Tag = EDDConfig.Instance.ConvertTimeToUTCFromSelected(selectedtime);
            rw.Cells[ColTags.Index].Tag = "";

            dataGridView.Rows.Insert(0, rw);
            dataGridView.SetCurrentSelOnRow(0, 2);
            StoreRow(rw);
        }


        #endregion


        #region Filter

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            searchtimer.Stop();
            searchtimer.Start();
        }

        private void Searchtimer_Tick(object sender, EventArgs e)
        {
            searchtimer.Stop();
            FilterView();
        }

        private void buttonFilter_MouseClick(object sender, MouseEventArgs e)
        {
            string curtags = TagsForm.UniqueTags(GlobalCaptainsLogList.Instance.GetAllTags(EDCommander.CurrentCmdrID), EDDConfig.TagSplitStringCL);
            TagsForm.EditTags(this.FindForm(),
                                        EDDConfig.Instance.CaptainsLogTagDictionary, curtags, GetSetting(dbTags, "All"),
                                        buttonFilter.PointToScreen(new System.Drawing.Point(0, buttonFilter.Height)),
                                        TagFilterChanged, null, EDDConfig.TagSplitStringCL,
                                        true,// we want ALL back to include everything in case we don't know the tag (due to it being removed)
                                        true);          // and we want Or/empty

        }

        private void TagFilterChanged(string newtags, Object tag)
        {
            PutSetting(dbTags, newtags);
            FilterView();
        }

        private void FilterView()
        {
            string tags = GetSetting(dbTags, "All");
            dataGridView.FilterGridView((row) => row.IsTextInRow(textBoxFilter.Text) && TagsForm.AreTagsInFilter(row.Cells[ColTags.Index].Tag, tags, EDDConfig.TagSplitStringCL));
        }

        #endregion

        #region Reaction to bookmarks doing stuff from outside sources

        bool inupdate = false;
        private void LogChanged(CaptainsLogClass bk, bool deleted)
        {
            if (!inupdate)
            {
                if (dataGridView.IsCurrentCellInEditMode)
                    dataGridView.EndEdit();
                Display();
            }
        }

        #endregion

        #region Right clicks

        CaptainsLogClass rightclickentry = null;

        private void dataGridView_MouseDown(object sender, MouseEventArgs e)
        {
            rightclickentry = dataGridView.RightClickRowValid ? (CaptainsLogClass)dataGridView.Rows[dataGridView.RightClickRow].Tag : null;
        }

        private void contextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            toolStripMenuItemGotoStar3dmap.Enabled = rightclickentry != null;
            openInEDSMToolStripMenuItem.Enabled = rightclickentry != null;
            openAScanPanelViewToolStripMenuItem.Enabled = rightclickentry != null;
        }

        private void toolStripMenuItemGotoStar3dmap_Click(object sender, EventArgs e)
        {
            EliteDangerousCore.ISystem s = SystemCache.FindSystem(rightclickentry.SystemName, DiscoveryForm.GalacticMapping, EliteDangerousCore.WebExternalDataLookup.All);
            DiscoveryForm.Open3DMap(s);
        }

        private void openInEDSMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            EliteDangerousCore.EDSM.EDSMClass edsm = new EDSMClass();
            
            if (!edsm.ShowSystemInEDSM(rightclickentry.SystemName))
                ExtendedControls.MessageBoxTheme.Show(FindForm(), "System could not be found - has not been synched or EDSM is unavailable".T(EDTx.CaptainsLogEntries_SysU));

            this.Cursor = Cursors.Default;
        }

        private void openAScanPanelViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ISystem sys = SystemCache.FindSystem(rightclickentry.SystemName, DiscoveryForm.GalacticMapping, EliteDangerousCore.WebExternalDataLookup.All);

            if ( sys != null )
                ScanDisplayForm.ShowScanOrMarketForm(this.FindForm(), sys, DiscoveryForm.History);
            else
                ExtendedControls.MessageBoxTheme.Show(this.FindForm(), "No such system".T(EDTx.CaptainsLogEntries_NSS) + " " + rightclickentry.SystemName, "Warning".T(EDTx.Warning), MessageBoxButtons.OK, MessageBoxIcon.Error);

        }

        #endregion

        #region Excel

        private void extButtonExcel_Click(object sender, EventArgs e)
        {
            Forms.ImportExportForm frm = new Forms.ImportExportForm();
            frm.Export( new string[] { "Export Current View", "All" }, new Forms.ImportExportForm.ShowFlags[] { Forms.ImportExportForm.ShowFlags.ShowCSVOpenInclude, Forms.ImportExportForm.ShowFlags.ShowCSVOpenInclude });

            if (frm.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                BaseUtils.CSVWriteGrid grd = new BaseUtils.CSVWriteGrid(frm.Delimiter);


                grd.GetLineHeader += delegate (int c)
                {
                    if (c == 0)
                        return new string[] { "Time", "System","Body","Note","Tags" };
                    else
                        return null;
                };

                if (frm.SelectedIndex == 1)
                {
                    List<CaptainsLogClass> logs = GlobalCaptainsLogList.Instance.LogEntries;
                    int i = 0;

                    grd.GetLine += delegate (int r)
                    {
                        while (i < logs.Count)
                        {
                            CaptainsLogClass ent = logs[i++];
                            if (ent.Commander == EDCommander.CurrentCmdrID)
                            {
                                return new object[] { EDDConfig.Instance.ConvertTimeToSelectedFromUTC(ent.TimeUTC),
                                                      ent.SystemName , ent.BodyName, ent.Note, ent.Tags };
                            }
                        }

                        return null;
                    };
                }
                else
                {
                    grd.GetLine += delegate (int r)
                    {
                        if (r < dataGridView.RowCount)
                        {
                            DataGridViewRow rw = dataGridView.Rows[r];
                            CaptainsLogClass ent = rw.Tag as CaptainsLogClass;
                            return new Object[] { rw.Cells[0].Value, rw.Cells[1].Value, rw.Cells[2].Value, rw.Cells[3].Value, ent.Tags};
                        }

                        return null;
                    };

                }

                grd.WriteGrid(frm.Path, frm.AutoOpen, FindForm());
            }

        }

        #endregion

    }
}
