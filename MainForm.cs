using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace LightweightAutoClicker
{
    internal sealed class MainForm : Form
    {
        private const int ToggleHotKeyId = 0xAC61;
        private const int CaptureHotKeyId = 0xAC62;
        private const int WmHotKey = 0x0312;

        private readonly ComboBox windowCombo = new ComboBox();
        private readonly ComboBox groupCombo = new ComboBox();
        private readonly Button refreshButton = new Button();
        private readonly Button addGroupButton = new Button();
        private readonly Button renameGroupButton = new Button();
        private readonly Button deleteGroupButton = new Button();
        private readonly Button addButton = new Button();
        private readonly Button removeButton = new Button();
        private readonly Button testButton = new Button();
        private readonly Button startButton = new Button();
        private readonly Button stopButton = new Button();
        private readonly CheckBox topMostCheck = new CheckBox();
        private readonly CheckBox groupEnabledCheck = new CheckBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly ClickVisualizer visualizer = new ClickVisualizer();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel countLabel = new ToolStripStatusLabel();
        private readonly Timer uiTimer = new Timer();
        private readonly ClickEngine engine = new ClickEngine();
        private readonly List<ClickGroupConfig> groups = new List<ClickGroupConfig>();
        private readonly string settingsPath;
        private AppSettings loadedSettings;
        private ClickGroupConfig currentGroup;
        private bool loadingGroups;
        private bool toggleHotKeyRegistered;
        private bool captureHotKeyRegistered;
        private long displayedClickId;

        public MainForm()
        {
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LightweightAutoClicker",
                "settings.xml");

            Text = "AUTO CLICKER  by：一叶丶知秋";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 500);
            Size = new Size(1060, 620);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = LoadApplicationIcon();

            BuildUi();
            LoadSettings();
            RefreshWindows();

            uiTimer.Interval = 100;
            uiTimer.Tick += delegate { UpdateStatus(); };
            FormClosing += OnFormClosing;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            toggleHotKeyRegistered = NativeMethods.RegisterHotKey(Handle, ToggleHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F6);
            captureHotKeyRegistered = NativeMethods.RegisterHotKey(Handle, CaptureHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F8);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (toggleHotKeyRegistered)
                NativeMethods.UnregisterHotKey(Handle, ToggleHotKeyId);
            if (captureHotKeyRegistered)
                NativeMethods.UnregisterHotKey(Handle, CaptureHotKeyId);
            toggleHotKeyRegistered = false;
            captureHotKeyRegistered = false;
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotKey)
            {
                if (m.WParam.ToInt32() == ToggleHotKeyId)
                {
                    ToggleRunning();
                    return;
                }
                if (m.WParam.ToInt32() == CaptureHotKeyId)
                {
                    CapturePointAtCursor();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var settingsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 6,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 10)
            };
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            settingsPanel.Controls.Add(new Label { Text = "目标窗口：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            windowCombo.Dock = DockStyle.Fill;
            windowCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            windowCombo.SelectedIndexChanged += delegate { UpdatePreview(); };
            settingsPanel.Controls.Add(windowCombo, 1, 0);

            refreshButton.Text = "刷新窗口";
            refreshButton.AutoSize = true;
            refreshButton.Click += delegate { RefreshWindows(); };
            settingsPanel.Controls.Add(refreshButton, 2, 0);

            topMostCheck.Text = "置顶";
            topMostCheck.AutoSize = true;
            topMostCheck.Anchor = AnchorStyles.Left;
            topMostCheck.CheckedChanged += delegate { TopMost = topMostCheck.Checked; };
            settingsPanel.Controls.Add(topMostCheck, 3, 0);

            settingsPanel.Controls.Add(new Label { Text = "连点分组：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            groupCombo.Dock = DockStyle.Fill;
            groupCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            groupCombo.SelectedIndexChanged += OnGroupChanged;
            settingsPanel.Controls.Add(groupCombo, 1, 1);

            groupEnabledCheck.Text = "组启用";
            groupEnabledCheck.AutoSize = true;
            groupEnabledCheck.CheckedChanged += OnGroupEnabledChanged;
            settingsPanel.Controls.Add(groupEnabledCheck, 2, 1);

            addGroupButton.Text = "新建分组";
            addGroupButton.AutoSize = true;
            addGroupButton.Click += delegate { CreateGroup(); };
            settingsPanel.Controls.Add(addGroupButton, 3, 1);

            renameGroupButton.Text = "重命名";
            renameGroupButton.AutoSize = true;
            renameGroupButton.Click += delegate { RenameCurrentGroup(); };
            settingsPanel.Controls.Add(renameGroupButton, 4, 1);

            deleteGroupButton.Text = "删除分组";
            deleteGroupButton.AutoSize = true;
            deleteGroupButton.Click += delegate { DeleteCurrentGroup(); };
            settingsPanel.Controls.Add(deleteGroupButton, 5, 1);
            root.Controls.Add(settingsPanel, 0, 0);

            ConfigureGrid();
            var workArea = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            workArea.Panel1.Padding = new Padding(0, 0, 8, 0);
            workArea.Panel1.Controls.Add(grid);
            var previewLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "点击预览",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText
            };
            visualizer.Dock = DockStyle.Fill;
            workArea.Panel2.Controls.Add(visualizer);
            workArea.Panel2.Controls.Add(previewLabel);
            workArea.HandleCreated += delegate
            {
                BeginInvoke(new Action(delegate
                {
                    if (workArea.IsDisposed || workArea.ClientSize.Width <= 0)
                        return;
                    workArea.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
                    workArea.Panel2MinSize = 220;
                    workArea.SplitterDistance = Math.Max(360, workArea.ClientSize.Width - 300);
                }));
            };
            root.Controls.Add(workArea, 0, 1);

            var editPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 6)
            };
            editPanel.Controls.Add(new Label
            {
                Text = "鼠标停在目标位置后按 F8 记录坐标",
                AutoSize = true,
                Padding = new Padding(0, 6, 12, 0),
                ForeColor = SystemColors.GrayText
            });
            addButton.Text = "添加空白点";
            addButton.AutoSize = true;
            addButton.Click += delegate { AddBlankPoint(); };
            editPanel.Controls.Add(addButton);

            removeButton.Text = "删除选中";
            removeButton.AutoSize = true;
            removeButton.Click += delegate { RemoveSelectedRows(); };
            editPanel.Controls.Add(removeButton);

            testButton.Text = "测试选中点";
            testButton.AutoSize = true;
            testButton.Click += delegate { TestSelectedPoint(); };
            editPanel.Controls.Add(testButton);
            root.Controls.Add(editPanel, 0, 2);

            var actionPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                Margin = new Padding(0, 4, 0, 0)
            };
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionPanel.Controls.Add(new Label
            {
                Text = "各启用分组中的点击点会独立触发。热键：F6 启停，F8 取点",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left
            }, 0, 0);

            startButton.Text = "开始 (F6)";
            startButton.AutoSize = true;
            startButton.Padding = new Padding(12, 4, 12, 4);
            startButton.Click += delegate { StartClicking(); };
            actionPanel.Controls.Add(startButton, 1, 0);

            stopButton.Text = "停止 (F6)";
            stopButton.AutoSize = true;
            stopButton.Padding = new Padding(12, 4, 12, 4);
            stopButton.Enabled = false;
            stopButton.Click += delegate { StopClicking(); };
            actionPanel.Controls.Add(stopButton, 2, 0);
            root.Controls.Add(actionPanel, 0, 3);

            statusStrip.SizingGrip = false;
            statusLabel.Text = "就绪";
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            countLabel.Text = "已发送 0 次";
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(countLabel);
            Controls.Add(statusStrip);
        }

        private void ConfigureGrid()
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = true;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.MultiSelect = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "启用", Width = 55, TrueValue = true, FalseValue = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PointName", HeaderText = "名称", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 28 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "X", HeaderText = "X", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Y", HeaderText = "Y", Width = 80 });
            var buttonColumn = new DataGridViewComboBoxColumn
            {
                Name = "Button",
                HeaderText = "鼠标键",
                Width = 100,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            buttonColumn.Items.AddRange("左键", "右键", "中键");
            grid.Columns.Add(buttonColumn);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Interval", HeaderText = "间隔 (ms)", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "约 次/秒", Width = 90, ReadOnly = true });

            grid.CellEndEdit += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                {
                    UpdateRateCell(grid.Rows[e.RowIndex]);
                    PersistCurrentGroup();
                }
            };
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["Enabled"].Index)
                    PersistCurrentGroup();
            };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
        }

        private void RefreshWindows()
        {
            IntPtr selectedHandle = GetSelectedWindowHandle();
            string preferredProcess = loadedSettings == null ? string.Empty : loadedSettings.TargetProcessName;
            string preferredTitle = loadedSettings == null ? string.Empty : loadedSettings.TargetWindowTitle;
            List<WindowInfo> windows = NativeMethods.GetTopLevelWindows(Handle);

            windowCombo.BeginUpdate();
            windowCombo.Items.Clear();
            foreach (WindowInfo window in windows)
                windowCombo.Items.Add(window);
            windowCombo.EndUpdate();

            int index = -1;
            for (int i = 0; i < windows.Count; i++)
            {
                if (selectedHandle != IntPtr.Zero && windows[i].Handle == selectedHandle)
                {
                    index = i;
                    break;
                }
                if (index < 0 && preferredProcess.Length > 0 &&
                    string.Equals(windows[i].ProcessName, preferredProcess, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(windows[i].Title, preferredTitle, StringComparison.Ordinal))
                    index = i;
            }

            if (index >= 0)
                windowCombo.SelectedIndex = index;
            else if (windowCombo.Items.Count > 0)
                windowCombo.SelectedIndex = 0;

            loadedSettings = null;
            UpdatePreview();
            statusLabel.Text = string.Format("发现 {0} 个可选窗口", windows.Count);
        }

        private void CapturePointAtCursor()
        {
            if (engine.IsRunning)
            {
                statusLabel.Text = "运行中不能取点，请先停止";
                return;
            }

            IntPtr target = GetSelectedWindowHandle();
            if (target == IntPtr.Zero)
            {
                statusLabel.Text = "请先选择目标窗口";
                return;
            }

            Point screen = Cursor.Position;
            int x;
            int y;
            if (!NativeMethods.TryConvertScreenToClient(target, screen.X, screen.Y, out x, out y))
            {
                statusLabel.Text = "取点失败：请将鼠标停在目标窗口客户区内后按 F8";
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            AddPointToGrid(new ClickPointConfig
            {
                Name = "点击点 " + (grid.Rows.Count + 1),
                X = x,
                Y = y,
                IntervalMs = 1000,
                Button = ClickButton.Left,
                Enabled = true
            });
            PersistCurrentGroup();
            statusLabel.Text = string.Format("已添加 {0}：({1}, {2})", currentGroup.Name, x, y);
        }

        private void AddBlankPoint()
        {
            AddPointToGrid(new ClickPointConfig { Name = "点击点 " + (grid.Rows.Count + 1) });
            PersistCurrentGroup();
        }

        private void AddPointToGrid(ClickPointConfig point)
        {
            int index = grid.Rows.Add(point.Enabled, point.Name, point.X, point.Y, ButtonToText(point.Button), point.IntervalMs, string.Empty);
            UpdateRateCell(grid.Rows[index]);
            grid.ClearSelection();
            grid.Rows[index].Selected = true;
        }

        private void RemoveSelectedRows()
        {
            var rows = grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(row => row.Index).ToList();
            foreach (DataGridViewRow row in rows)
                if (!row.IsNewRow)
                    grid.Rows.RemoveAt(row.Index);
            PersistCurrentGroup();
        }

        private void TestSelectedPoint()
        {
            WindowInfo selected = GetSelectedWindowInfo();
            IntPtr target = selected == null ? IntPtr.Zero : selected.Handle;
            if (target == IntPtr.Zero || !NativeMethods.IsWindow(target))
            {
                statusLabel.Text = "目标窗口已失效，请刷新并重新选择";
                return;
            }
            if (grid.SelectedRows.Count == 0)
            {
                statusLabel.Text = "请先选择一个点击点";
                return;
            }

            ClickPointConfig point;
            string error;
            if (!TryReadRow(grid.SelectedRows[0], out point, out error))
            {
                statusLabel.Text = error;
                return;
            }

            IClickDispatcher dispatcher;
            string dispatcherError;
            if (!ClickDispatcherFactory.TryCreate(selected, out dispatcher, out dispatcherError))
            {
                statusLabel.Text = dispatcherError;
                return;
            }

            bool sent;
            string mode = dispatcher.ModeName;
            using (dispatcher)
                sent = dispatcher.TryClick(point);
            if (sent)
                visualizer.Pulse(point.X, point.Y);
            statusLabel.Text = sent ? string.Format("已向 {0} 发送一次测试点击（{1}）", point.Name, mode) : "测试点击发送失败";
        }

        private void ToggleRunning()
        {
            if (engine.IsRunning)
                StopClicking();
            else
                StartClicking();
        }

        private void StartClicking()
        {
            grid.EndEdit();
            string error;
            if (!CommitCurrentGroup(out error))
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WindowInfo selected = GetSelectedWindowInfo();
            IntPtr target = selected == null ? IntPtr.Zero : selected.Handle;
            if (target == IntPtr.Zero || !NativeMethods.IsWindow(target))
            {
                MessageBox.Show(this, "目标窗口已失效，请刷新窗口列表。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<ClickPointConfig> points = GetRunnablePoints();
            if (points.Count == 0)
            {
                MessageBox.Show(this, "至少需要在启用分组中启用一个点击点。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            IClickDispatcher dispatcher;
            string dispatcherError;
            if (!ClickDispatcherFactory.TryCreate(selected, out dispatcher, out dispatcherError))
            {
                MessageBox.Show(this, dispatcherError, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string mode = dispatcher.ModeName;
            engine.Start(dispatcher, points);
            displayedClickId = 0;
            uiTimer.Start();
            SetEditingEnabled(false);
            UpdatePreview();
            statusLabel.Text = string.Format("运行中：{0} 个点击点（{1}）", points.Count, mode);
        }

        private void StopClicking()
        {
            engine.Stop();
            uiTimer.Stop();
            SetEditingEnabled(true);
            statusLabel.Text = "已停止";
        }

        private void CreateGroup()
        {
            string error;
            if (!CommitCurrentGroup(out error))
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var group = new ClickGroupConfig { Name = NextGroupName() };
            groups.Add(group);
            SelectGroup(group);
            SaveSettings();
        }

        private void RenameCurrentGroup()
        {
            if (currentGroup == null)
                return;

            string value = PromptForText("重命名分组", "分组名称：", currentGroup.Name);
            if (value == null)
                return;
            value = value.Trim();
            if (value.Length == 0)
            {
                MessageBox.Show(this, "分组名称不能为空。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (groups.Any(group => group != currentGroup && string.Equals(group.Name, value, StringComparison.CurrentCultureIgnoreCase)))
            {
                MessageBox.Show(this, "分组名称已存在。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            currentGroup.Name = value;
            int index = groupCombo.SelectedIndex;
            groupCombo.Items[index] = currentGroup;
            SaveSettings();
        }

        private void DeleteCurrentGroup()
        {
            if (currentGroup == null || groups.Count <= 1)
            {
                MessageBox.Show(this, "至少保留一个分组。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, string.Format("删除分组“{0}”及其中的所有点击点？", currentGroup.Name), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int index = groups.IndexOf(currentGroup);
            groups.Remove(currentGroup);
            SelectGroup(groups[Math.Max(0, index - 1)]);
            SaveSettings();
        }

        private void OnGroupChanged(object sender, EventArgs e)
        {
            if (loadingGroups)
                return;
            var selected = groupCombo.SelectedItem as ClickGroupConfig;
            if (selected == null || selected == currentGroup)
                return;

            string error;
            if (!CommitCurrentGroup(out error))
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SelectGroup(currentGroup);
                return;
            }
            currentGroup = selected;
            LoadCurrentGroupIntoGrid();
            UpdatePreview();
        }

        private void OnGroupEnabledChanged(object sender, EventArgs e)
        {
            if (loadingGroups || currentGroup == null)
                return;
            currentGroup.Enabled = groupEnabledCheck.Checked;
            UpdatePreview();
            SaveSettings();
        }

        private void SelectGroup(ClickGroupConfig group)
        {
            currentGroup = group;
            loadingGroups = true;
            groupCombo.BeginUpdate();
            groupCombo.Items.Clear();
            foreach (ClickGroupConfig item in groups)
                groupCombo.Items.Add(item);
            groupCombo.SelectedItem = group;
            groupEnabledCheck.Checked = group.Enabled;
            groupCombo.EndUpdate();
            loadingGroups = false;
            LoadCurrentGroupIntoGrid();
            UpdatePreview();
        }

        private void LoadCurrentGroupIntoGrid()
        {
            grid.Rows.Clear();
            if (currentGroup == null)
                return;
            foreach (ClickPointConfig point in currentGroup.Points)
                AddPointToGrid(point);
        }

        private bool CommitCurrentGroup(out string error)
        {
            error = string.Empty;
            if (currentGroup == null)
                return true;

            var points = new List<ClickPointConfig>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                ClickPointConfig point;
                if (!TryReadRow(row, out point, out error))
                    return false;
                points.Add(point);
            }
            currentGroup.Points = points;
            return true;
        }

        private void PersistCurrentGroup()
        {
            string error;
            if (!CommitCurrentGroup(out error))
                return;
            UpdatePreview();
            SaveSettings();
        }

        private List<ClickPointConfig> GetRunnablePoints()
        {
            var points = new List<ClickPointConfig>();
            foreach (ClickGroupConfig group in groups)
            {
                if (!group.Enabled)
                    continue;
                foreach (ClickPointConfig point in group.Points)
                    if (point.Enabled)
                        points.Add(point.Copy());
            }
            return points;
        }

        private void SetEditingEnabled(bool enabled)
        {
            windowCombo.Enabled = enabled;
            refreshButton.Enabled = enabled;
            groupCombo.Enabled = enabled;
            groupEnabledCheck.Enabled = enabled;
            addGroupButton.Enabled = enabled;
            renameGroupButton.Enabled = enabled;
            deleteGroupButton.Enabled = enabled;
            addButton.Enabled = enabled;
            removeButton.Enabled = enabled;
            testButton.Enabled = enabled;
            grid.ReadOnly = !enabled;
            startButton.Enabled = enabled;
            stopButton.Enabled = !enabled;
        }

        private bool TryReadRow(DataGridViewRow row, out ClickPointConfig point, out string error)
        {
            point = null;
            error = string.Empty;
            int x;
            int y;
            int interval;
            string rowName = Convert.ToString(row.Cells["PointName"].Value).Trim();

            if (!int.TryParse(Convert.ToString(row.Cells["X"].Value), out x) || !int.TryParse(Convert.ToString(row.Cells["Y"].Value), out y))
            {
                error = string.Format("第 {0} 行的坐标必须是整数。", row.Index + 1);
                return false;
            }
            if (!int.TryParse(Convert.ToString(row.Cells["Interval"].Value), out interval) || interval < 10 || interval > 86400000)
            {
                error = string.Format("第 {0} 行的间隔必须在 10 到 86400000 毫秒之间。", row.Index + 1);
                return false;
            }

            point = new ClickPointConfig
            {
                Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value ?? false),
                Name = rowName.Length == 0 ? "点击点 " + (row.Index + 1) : rowName,
                X = x,
                Y = y,
                Button = TextToButton(Convert.ToString(row.Cells["Button"].Value)),
                IntervalMs = interval
            };
            return true;
        }

        private void UpdateRateCell(DataGridViewRow row)
        {
            int interval;
            row.Cells["Rate"].Value = int.TryParse(Convert.ToString(row.Cells["Interval"].Value), out interval) && interval > 0
                ? (1000.0 / interval).ToString("0.##")
                : "—";
        }

        private void UpdatePreview()
        {
            int width = 1;
            int height = 1;
            NativeMethods.TryGetClientSize(GetSelectedWindowHandle(), out width, out height);
            visualizer.SetPoints(GetRunnablePoints(), width, height);
        }

        private void UpdateStatus()
        {
            countLabel.Text = string.Format("已发送 {0:N0} 次", engine.TotalClicks);
            int x;
            int y;
            long clickId;
            if (engine.TryGetLastClick(out x, out y, out clickId) && clickId != displayedClickId)
            {
                displayedClickId = clickId;
                visualizer.Pulse(x, y);
            }
            visualizer.Advance();

            if (engine.IsRunning && !NativeMethods.IsWindow(GetSelectedWindowHandle()))
            {
                StopClicking();
                statusLabel.Text = "目标窗口已关闭，任务自动停止";
            }
        }

        private WindowInfo GetSelectedWindowInfo()
        {
            return windowCombo.SelectedItem as WindowInfo;
        }

        private IntPtr GetSelectedWindowHandle()
        {
            WindowInfo selected = GetSelectedWindowInfo();
            return selected == null ? IntPtr.Zero : selected.Handle;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    using (var stream = File.OpenRead(settingsPath))
                        loadedSettings = (AppSettings)serializer.Deserialize(stream);
                }
                else
                {
                    loadedSettings = new AppSettings();
                }

                topMostCheck.Checked = loadedSettings.AlwaysOnTop;
                if (loadedSettings.Groups != null && loadedSettings.Groups.Count > 0)
                {
                    foreach (ClickGroupConfig group in loadedSettings.Groups)
                        groups.Add(group);
                }
                else
                {
                    var migrated = new ClickGroupConfig { Name = "默认分组" };
                    if (loadedSettings.Points != null)
                        migrated.Points.AddRange(loadedSettings.Points);
                    groups.Add(migrated);
                }

                if (groups.Count == 0)
                    groups.Add(new ClickGroupConfig { Name = "默认分组" });
                SelectGroup(groups[0]);
            }
            catch
            {
                loadedSettings = new AppSettings();
                groups.Clear();
                groups.Add(new ClickGroupConfig { Name = "默认分组" });
                SelectGroup(groups[0]);
                statusLabel.Text = "旧配置读取失败，已使用默认配置";
            }
        }

        private void SaveSettings()
        {
            try
            {
                string error;
                if (!CommitCurrentGroup(out error))
                    return;

                var selected = GetSelectedWindowInfo();
                var settings = new AppSettings
                {
                    AlwaysOnTop = topMostCheck.Checked,
                    TargetProcessName = selected == null ? string.Empty : selected.ProcessName,
                    TargetWindowTitle = selected == null ? string.Empty : selected.Title,
                    Points = new List<ClickPointConfig>(),
                    Groups = groups.Select(group => group.Copy()).ToList()
                };

                string directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                var serializer = new XmlSerializer(typeof(AppSettings));
                using (var stream = File.Create(settingsPath))
                    serializer.Serialize(stream, settings);
            }
            catch
            {
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            uiTimer.Stop();
            engine.Stop();
            SaveSettings();
            engine.Dispose();
        }

        private string NextGroupName()
        {
            int index = 1;
            string name;
            do
            {
                name = "分组 " + index++;
            }
            while (groups.Any(group => string.Equals(group.Name, name, StringComparison.CurrentCultureIgnoreCase)));
            return name;
        }

        private static string PromptForText(string title, string labelText, string value)
        {
            using (var dialog = new Form())
            using (var label = new Label())
            using (var input = new TextBox())
            using (var confirm = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = title;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(340, 116);

                label.Text = labelText;
                label.SetBounds(14, 14, 310, 20);
                input.Text = value;
                input.SetBounds(14, 38, 312, 26);
                confirm.Text = "确定";
                confirm.DialogResult = DialogResult.OK;
                confirm.SetBounds(170, 78, 74, 28);
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(252, 78, 74, 28);
                dialog.AcceptButton = confirm;
                dialog.CancelButton = cancel;
                dialog.Controls.AddRange(new Control[] { label, input, confirm, cancel });
                dialog.Shown += delegate { input.SelectAll(); input.Focus(); };
                return dialog.ShowDialog() == DialogResult.OK ? input.Text : null;
            }
        }

        private static string ButtonToText(ClickButton button)
        {
            switch (button)
            {
                case ClickButton.Right: return "右键";
                case ClickButton.Middle: return "中键";
                default: return "左键";
            }
        }

        private static ClickButton TextToButton(string text)
        {
            if (text == "右键") return ClickButton.Right;
            if (text == "中键") return ClickButton.Middle;
            return ClickButton.Left;
        }

        private static Icon LoadApplicationIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                    return icon;
            }
            catch
            {
            }
            return SystemIcons.Application;
        }
    }
}
