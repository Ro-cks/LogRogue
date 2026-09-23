using System.ComponentModel;
using LogRogue.Core.Deletion;
using LogRogue.Core.Scheduling;

namespace LogRogue.App;

/// <summary>
/// 예약 실행 설정 창.
/// [확인]을 누르면 DialogResult.OK와 함께 Result에 바뀐 설정이 담긴다.
///
/// PreviewForm, HistoryForm과 마찬가지로 디자이너 없이 코드로 만든 창이다.
/// </summary>
[DesignerCategory("Code")]
public sealed class ScheduleForm : Form
{
    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    private sealed record ModeOption(string Text, ScheduleMode Mode)
    {
        public override string ToString() => Text;
    }

    private readonly CheckBox _enabled = new() { Text = "예약 실행 사용", AutoSize = true };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly DateTimePicker _time = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm",
        ShowUpDown = true,   // 달력 대신 위아래 화살표로 시각을 고른다
        Width = 80
    };
    private readonly NumericUpDown _interval = new()
    {
        Minimum = ScheduleSettings.MinIntervalHours,
        Maximum = ScheduleSettings.MaxIntervalHours,
        Width = 60
    };
    private readonly Dictionary<DayOfWeek, CheckBox> _weekdays = new();
    private readonly CheckBox _catchUp = new() { Text = "PC가 꺼져 있어 지나간 예약은 켜진 뒤에 실행", AutoSize = true };
    private readonly CheckBox _notifyOnlyOnProblem = new() { Text = "문제가 있을 때만 알림", AutoSize = true };
    private readonly Label _next = new() { AutoSize = true };
    private readonly Label _warning = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(420, 0) };

    private readonly DateTime? _lastRunAt;
    private bool _loading = true;

    public ScheduleForm(ScheduleSettings current, DeleteMode deleteMode)
    {
        _lastRunAt = current.LastRunAt;

        SuspendLayout();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = "예약 설정";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(14);

        _mode.Items.Add(new ModeOption("매일", ScheduleMode.Daily));
        _mode.Items.Add(new ModeOption("매주", ScheduleMode.Weekly));
        _mode.Items.Add(new ModeOption("시간 간격", ScheduleMode.Interval));

        FlowLayoutPanel body = BuildBody();
        FlowLayoutPanel buttons = CreateButtons(out Button okButton, out Button cancelButton);

        Controls.Add(body);
        Controls.Add(buttons);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadFrom(current, deleteMode);

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>[확인]을 눌렀을 때 화면에 입력된 설정.</summary>
    public ScheduleSettings Result => Build();

    // ── 화면 구성 ────────────────────────────────────────

    private FlowLayoutPanel BuildBody()
    {
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        body.Controls.Add(_enabled);
        body.Controls.Add(Spacer(6));
        body.Controls.Add(Row(new Label { Text = "주기", AutoSize = true, Padding = new Padding(0, 6, 6, 0) }, _mode));
        body.Controls.Add(Row(new Label { Text = "시각", AutoSize = true, Padding = new Padding(0, 6, 6, 0) }, _time));
        body.Controls.Add(Row(
            new Label { Text = "간격", AutoSize = true, Padding = new Padding(0, 6, 6, 0) },
            _interval,
            new Label { Text = "시간마다", AutoSize = true, Padding = new Padding(4, 6, 0, 0) }));

        var weekdayRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 2) };
        weekdayRow.Controls.Add(new Label { Text = "요일", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });

        foreach (DayOfWeek day in WeekOrder)
        {
            var box = new CheckBox
            {
                Text = ScheduleCalculator.ShortName(day),
                AutoSize = true,
                Margin = new Padding(0, 4, 6, 0)
            };
            box.CheckedChanged += (_, _) => UpdateState();

            _weekdays[day] = box;
            weekdayRow.Controls.Add(box);
        }

        body.Controls.Add(weekdayRow);
        body.Controls.Add(Spacer(8));
        body.Controls.Add(_catchUp);
        body.Controls.Add(_notifyOnlyOnProblem);
        body.Controls.Add(Spacer(10));
        body.Controls.Add(_next);
        body.Controls.Add(_warning);

        _enabled.CheckedChanged += (_, _) => UpdateState();
        _mode.SelectedIndexChanged += (_, _) => UpdateState();
        _time.ValueChanged += (_, _) => UpdateState();
        _interval.ValueChanged += (_, _) => UpdateState();

        return body;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.AddRange(controls);
        return row;
    }

    private static Control Spacer(int height) => new Label { Text = "", AutoSize = false, Height = height, Width = 1 };

    private FlowLayoutPanel CreateButtons(out Button okButton, out Button cancelButton)
    {
        okButton = new Button { Text = "확인", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 28) };
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

    private void LoadFrom(ScheduleSettings current, DeleteMode deleteMode)
    {
        _loading = true;

        _enabled.Checked = current.Enabled;

        foreach (object item in _mode.Items)
        {
            if (item is ModeOption option && option.Mode == current.Mode)
            {
                _mode.SelectedItem = item;
                break;
            }
        }

        _mode.SelectedItem ??= _mode.Items[0];

        // DateTimePicker는 날짜까지 가진 컨트롤이라 오늘 날짜에 시각만 얹어서 넣는다
        _time.Value = DateTime.Today + current.TimeOfDay.ToTimeSpan();
        _interval.Value = Math.Clamp(current.IntervalHours, _interval.Minimum, _interval.Maximum);

        foreach (DayOfWeek day in WeekOrder)
            _weekdays[day].Checked = current.Weekdays.Contains(day);

        _catchUp.Checked = current.CatchUpMissed;
        _notifyOnlyOnProblem.Checked = current.NotifyOnlyOnProblem;

        _warning.Text = deleteMode == DeleteMode.Permanent
            ? "원본 삭제가 영구 삭제로 설정되어 있습니다.\n" +
              "예약 실행은 확인 창 없이 설정대로 원본을 영구 삭제합니다.\n" +
              "압축과 검증에 성공한 날짜만 지우므로 압축 파일에서 복원할 수 있습니다."
            : "";

        _loading = false;
        UpdateState();
    }

    private ScheduleSettings Build() => new()
    {
        Enabled = _enabled.Checked,
        Mode = _mode.SelectedItem is ModeOption option ? option.Mode : ScheduleMode.Daily,
        TimeOfDay = TimeOnly.FromDateTime(_time.Value),
        Weekdays = WeekOrder.Where(d => _weekdays[d].Checked).ToList(),
        IntervalHours = (int)_interval.Value,
        CatchUpMissed = _catchUp.Checked,
        NotifyOnlyOnProblem = _notifyOnlyOnProblem.Checked,
        LastRunAt = _lastRunAt
    };

    /// <summary>고른 주기에 맞는 컨트롤만 켜고, 다음 실행 시각을 미리 보여준다.</summary>
    private void UpdateState()
    {
        if (_loading)
            return;

        bool on = _enabled.Checked;
        ScheduleMode mode = _mode.SelectedItem is ModeOption option ? option.Mode : ScheduleMode.Daily;

        _mode.Enabled = on;
        _time.Enabled = on && mode is ScheduleMode.Daily or ScheduleMode.Weekly;
        _interval.Enabled = on && mode == ScheduleMode.Interval;
        _catchUp.Enabled = on;
        _notifyOnlyOnProblem.Enabled = on;

        foreach (CheckBox box in _weekdays.Values)
            box.Enabled = on && mode == ScheduleMode.Weekly;

        ScheduleSettings schedule = Build();
        DateTime? next = ScheduleCalculator.NextRun(schedule, DateTime.Now);

        _next.Text = !on
            ? "예약 실행 꺼짐"
            : next is null
                ? "요일을 하나 이상 선택하세요."
                : $"{ScheduleCalculator.Describe(schedule)}   ·   다음 실행: {next:yyyy-MM-dd(ddd) HH:mm}";

        _next.ForeColor = on && next is null ? Color.Firebrick : Color.Black;
        _warning.Visible = on && _warning.Text.Length > 0;
    }
}
