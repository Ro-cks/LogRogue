using System.ComponentModel;
using LogRogue.Core;
using LogRogue.Core.Archiving;
using LogRogue.Core.Deletion;
using LogRogue.Core.Jobs;
using LogRogue.Core.Scheduling;

namespace LogRogue.App;

/// <summary>
/// 작업 하나를 추가하거나 편집하는 창.
/// [확인]을 누르면 다른 작업과의 충돌까지 검사하고, 통과해야만 닫힌다.
/// 닫힌 뒤 Result에 편집된 작업이 담긴다.
///
/// 다른 보조 창들과 마찬가지로 디자이너 없이 코드로 만든 창이다.
/// </summary>
[DesignerCategory("Code")]
public sealed class JobForm : Form
{
    private sealed record Option<T>(string Text, T Value)
    {
        public override string ToString() => Text;
    }

    private readonly IReadOnlyList<JobConfig> _allJobs;
    private readonly string _id;
    private ScheduleSettings _schedule;

    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly TextBox _source = new() { Dock = DockStyle.Fill };
    private readonly TextBox _output = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _periodMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly DateTimePicker _start = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _end = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly NumericUpDown _keepDays = new()
    {
        Minimum = BackupPeriod.MinKeepRecentDays,
        Maximum = BackupPeriod.MaxKeepRecentDays,
        Width = 60
    };
    private readonly Label _periodHint = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly ComboBox _grouping = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly CheckBox _deleteSource = new() { Text = "원본 삭제", AutoSize = true, Margin = new Padding(0, 5, 8, 0) };
    private readonly ComboBox _deleteMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly Label _scheduleText = new() { AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
    private readonly Label _errors = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(520, 0) };

    /// <param name="job">편집할 작업. 복사본을 넘겨야 한다. 취소하면 버려진다.</param>
    /// <param name="allJobs">전체 작업 목록. 다른 작업과의 충돌 검사에 쓴다.</param>
    /// <param name="isNew">새로 추가하는 작업인지. 창 제목에만 쓴다.</param>
    public JobForm(JobConfig job, IReadOnlyList<JobConfig> allJobs, bool isNew)
    {
        _allJobs = allJobs;
        _id = job.Id;
        _schedule = job.Schedule.Clone();

        SuspendLayout();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = isNew ? "작업 추가" : $"작업 편집 - {job.Name}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 430);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(14);

        FillOptions();

        TableLayoutPanel table = BuildTable();
        FlowLayoutPanel buttons = CreateButtons(out Button okButton, out Button cancelButton);

        Controls.Add(table);
        Controls.Add(buttons);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadFrom(job);

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>[확인]으로 닫혔을 때 편집된 작업.</summary>
    public JobConfig Result { get; private set; } = null!;

    // ── 화면 구성 ────────────────────────────────────────

    private void FillOptions()
    {
        _periodMode.Items.Add(new Option<PeriodMode>("최근 며칠 제외 전부", PeriodMode.Relative));
        _periodMode.Items.Add(new Option<PeriodMode>("지정한 기간", PeriodMode.Absolute));

        _grouping.Items.Add(new Option<ArchiveGrouping>("일별", ArchiveGrouping.Daily));
        _grouping.Items.Add(new Option<ArchiveGrouping>("주별 (월~일)", ArchiveGrouping.Weekly));
        _grouping.Items.Add(new Option<ArchiveGrouping>("월별", ArchiveGrouping.Monthly));
        _grouping.Items.Add(new Option<ArchiveGrouping>("선택 기간 전체", ArchiveGrouping.WholeRange));

        _deleteMode.Items.Add(new Option<DeleteMode>("휴지통으로 이동", DeleteMode.RecycleBin));
        _deleteMode.Items.Add(new Option<DeleteMode>("영구 삭제", DeleteMode.Permanent));

        _periodMode.SelectedIndexChanged += (_, _) => UpdateState();
        _keepDays.ValueChanged += (_, _) => UpdateState();
        _start.ValueChanged += (_, _) => UpdateState();
        _end.ValueChanged += (_, _) => UpdateState();
        _deleteSource.CheckedChanged += (_, _) => UpdateState();
        _deleteMode.SelectedIndexChanged += (_, _) => UpdateState();
    }

    /// <summary>왼쪽 항목 이름, 가운데 입력칸, 오른쪽 버튼의 3칸 표.</summary>
    private TableLayoutPanel BuildTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddRow(table, "작업 이름", _name);
        AddRow(table, "대상 폴더", _source, BrowseButton(_source, "로그가 저장된 폴더를 선택하세요."));
        AddRow(table, "출력 폴더", _output, BrowseButton(_output, "압축 파일을 저장할 폴더를 선택하세요."));
        AddRow(table, "", Note("대상 폴더 안의 모든 파일이 수정한 날짜를 기준으로 압축됩니다."));

        AddRow(table, "기간", _periodMode);
        AddRow(table, "", Flow(new Label { Text = "최근", AutoSize = true, Margin = new Padding(0, 6, 4, 0) },
                                _keepDays,
                                new Label { Text = "일치는 남기고 그 이전 전부", AutoSize = true, Margin = new Padding(4, 6, 0, 0) }));
        AddRow(table, "", Flow(_start, new Label { Text = "~", AutoSize = true, Margin = new Padding(4, 6, 4, 0) }, _end));
        AddRow(table, "", _periodHint);

        AddRow(table, "압축 단위", _grouping);
        AddRow(table, "원본", Flow(_deleteSource, _deleteMode));

        var scheduleButton = new Button { Text = "예약 설정...", AutoSize = true };
        scheduleButton.Click += (_, _) => EditSchedule();
        AddRow(table, "예약", Flow(_scheduleText, scheduleButton));

        table.Controls.Add(_errors, 0, table.RowCount);
        table.SetColumnSpan(_errors, 3);
        table.RowCount++;

        return table;
    }

    private static void AddRow(TableLayoutPanel table, string caption, Control input, Control? button = null)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        table.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Margin = new Padding(0, 7, 10, 0)
        }, 0, row);

        table.Controls.Add(input, 1, row);

        if (button is not null)
            table.Controls.Add(button, 2, row);
        else
            table.SetColumnSpan(input, 2);
    }

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private static Label Note(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(3, 0, 0, 6)
    };

    private Button BrowseButton(TextBox target, string description)
    {
        var button = new Button { Text = "찾아보기", AutoSize = true };

        button.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true
            };

            if (Directory.Exists(target.Text.Trim()))
                dialog.SelectedPath = target.Text.Trim();

            if (dialog.ShowDialog(this) == DialogResult.OK)
                target.Text = dialog.SelectedPath;
        };

        return button;
    }

    private FlowLayoutPanel CreateButtons(out Button okButton, out Button cancelButton)
    {
        // 확인은 검사를 통과해야만 닫히도록 DialogResult를 직접 정한다
        okButton = new Button { Text = "확인", AutoSize = true, MinimumSize = new Size(90, 28) };
        okButton.Click += (_, _) => TryAccept();

        cancelButton = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 28) };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        panel.Controls.Add(cancelButton);
        panel.Controls.Add(okButton);
        return panel;
    }

    // ── 값 주고받기 ──────────────────────────────────────

    private void LoadFrom(JobConfig job)
    {
        _name.Text = job.Name;
        _source.Text = job.SourcePath;
        _output.Text = job.OutputPath;

        SelectOption(_periodMode, job.PeriodMode);
        _keepDays.Value = Math.Clamp(job.KeepRecentDays, _keepDays.Minimum, _keepDays.Maximum);
        _start.Value = job.StartDate.ToDateTime(TimeOnly.MinValue);
        _end.Value = job.EndDate.ToDateTime(TimeOnly.MinValue);

        SelectOption(_grouping, job.Grouping);
        SelectOption(_deleteMode, job.DeleteMode);
        _deleteSource.Checked = job.DeleteSource;

        UpdateState();
    }

    private JobConfig Build() => new()
    {
        Id = _id,
        Name = _name.Text.Trim(),
        SourcePath = _source.Text.Trim(),
        OutputPath = _output.Text.Trim(),
        PeriodMode = Selected(_periodMode, PeriodMode.Relative),
        StartDate = DateOnly.FromDateTime(_start.Value),
        EndDate = DateOnly.FromDateTime(_end.Value),
        KeepRecentDays = (int)_keepDays.Value,
        Grouping = Selected(_grouping, ArchiveGrouping.Daily),
        DeleteSource = _deleteSource.Checked,
        DeleteMode = Selected(_deleteMode, DeleteMode.RecycleBin),
        Schedule = _schedule.Clone()
    };

    private static void SelectOption<T>(ComboBox combo, T value)
    {
        foreach (object item in combo.Items)
        {
            if (item is Option<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static T Selected<T>(ComboBox combo, T fallback)
        => combo.SelectedItem is Option<T> option ? option.Value : fallback;

    /// <summary>고른 방식에 맞는 입력칸만 켜고, 기간과 예약 설명을 갱신한다.</summary>
    private void UpdateState()
    {
        bool relative = Selected(_periodMode, PeriodMode.Relative) == PeriodMode.Relative;
        _keepDays.Enabled = relative;
        _start.Enabled = !relative;
        _end.Enabled = !relative;

        BackupPeriod period = relative
            ? BackupPeriod.Relative((int)_keepDays.Value)
            : BackupPeriod.Absolute(DateOnly.FromDateTime(_start.Value), DateOnly.FromDateTime(_end.Value));

        _periodHint.Text = relative
            ? $"오늘 기준: {period.Describe()}"
            : "예약 실행에는 '최근 며칠 제외 전부'가 알맞습니다.";

        _deleteMode.Enabled = _deleteSource.Checked;

        DateTime? next = ScheduleCalculator.NextRun(_schedule, DateTime.Now);
        _scheduleText.Text = _schedule.Enabled && next is not null
            ? $"{ScheduleCalculator.Describe(_schedule)}  ·  다음 {next:MM-dd HH:mm}"
            : ScheduleCalculator.Describe(_schedule);
    }

    private void EditSchedule()
    {
        DeleteMode effective = _deleteSource.Checked ? Selected(_deleteMode, DeleteMode.RecycleBin) : DeleteMode.None;

        using var dialog = new ScheduleForm(_schedule.Clone(), effective);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _schedule = dialog.Result;
            UpdateState();
        }
    }

    private void TryAccept()
    {
        JobConfig job = Build();
        IReadOnlyList<string> errors = JobValidator.Validate(job, _allJobs);

        if (errors.Count > 0)
        {
            // 이유는 설명이 길어서 창 안에 다 들어가지 않는다.
            // 창 안에는 한 줄만 남기고, 전체 이유는 알림 창으로 보여준다.
            _errors.Text = errors.Count == 1
                ? "저장할 수 없습니다. 이유를 확인하고 설정을 고쳐주세요."
                : $"저장할 수 없습니다. (문제 {errors.Count}건) 이유를 확인하고 설정을 고쳐주세요.";

            MessageBox.Show(
                this,
                string.Join(Environment.NewLine + Environment.NewLine, errors.Select(e => "· " + e)),
                "작업을 저장할 수 없습니다",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        _errors.Text = "";

        Result = job;
        DialogResult = DialogResult.OK;
        Close();
    }
}
