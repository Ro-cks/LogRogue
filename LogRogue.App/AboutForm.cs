using System.ComponentModel;
using System.Diagnostics;
using LogRogue.Core;

namespace LogRogue.App;

/// <summary>
/// 프로그램 정보 창. 이름, 버전, 개발자, 연락처와 데이터 폴더 바로가기를 보여준다.
/// 표시되는 값은 AppInfo가 실행 파일에서 읽어온다.
///
/// 다른 보조 창들과 마찬가지로 디자이너 없이 코드로 만든 창이다.
/// </summary>
[DesignerCategory("Code")]
public sealed class AboutForm : Form
{
    public AboutForm(Icon appIcon)
    {
        SuspendLayout();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = $"{AppInfo.ProductName} 정보";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(16);

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        body.Controls.Add(CreateHeader(appIcon));
        body.Controls.Add(Gap(10));

        AddLine(body, "개발자", AppInfo.Developer);
        AddContact(body);
        AddLine(body, "소속", AppInfo.Company);

        if (AppInfo.Copyright.Length > 0)
        {
            body.Controls.Add(Gap(8));
            body.Controls.Add(new Label { Text = AppInfo.Copyright, AutoSize = true, ForeColor = Color.DimGray });
        }

        FlowLayoutPanel buttons = CreateButtons(out Button closeButton);

        Controls.Add(body);
        Controls.Add(buttons);

        AcceptButton = closeButton;
        CancelButton = closeButton;

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>아이콘 + 이름·버전·설명.</summary>
    private static Control CreateHeader(Icon appIcon)
    {
        var header = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

        var picture = new PictureBox
        {
            // 아이콘 파일에 담긴 여러 크기 중 48×48에 가장 가까운 것을 고른다
            Image = new Icon(appIcon, 48, 48).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(48, 48),
            Margin = new Padding(0, 0, 12, 0)
        };

        var texts = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        texts.Controls.Add(new Label
        {
            Text = AppInfo.ProductName,
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold)
        });

        texts.Controls.Add(new Label { Text = $"버전 {AppInfo.Version}", AutoSize = true });

        if (AppInfo.Description.Length > 0)
            texts.Controls.Add(new Label { Text = AppInfo.Description, AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(320, 0) });

        header.Controls.Add(picture);
        header.Controls.Add(texts);
        return header;
    }

    /// <summary>"개발자  홍길동" 같은 한 줄. 값이 비어 있으면 줄 자체를 넣지 않는다.</summary>
    private static void AddLine(FlowLayoutPanel body, string label, string value)
    {
        if (value.Length == 0)
            return;

        body.Controls.Add(Row(Caption(label), new Label { Text = value, AutoSize = true }));
    }

    /// <summary>연락처. 메일 주소나 웹 주소면 눌러서 바로 열 수 있게 한다.</summary>
    private static void AddContact(FlowLayoutPanel body)
    {
        string contact = AppInfo.Contact;
        if (contact.Length == 0)
            return;

        string? target = contact.Contains('@') && !contact.Contains("://")
            ? $"mailto:{contact}"
            : contact.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              contact.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? contact
                : null;

        if (target is null)
        {
            body.Controls.Add(Row(Caption("연락처"), new Label { Text = contact, AutoSize = true }));
            return;
        }

        var link = new LinkLabel { Text = contact, AutoSize = true };
        link.LinkClicked += (_, _) => OpenShell(target);

        body.Controls.Add(Row(Caption("연락처"), link));
    }

    private FlowLayoutPanel CreateButtons(out Button closeButton)
    {
        closeButton = new Button { Text = "닫기", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 28) };

        var dataButton = new Button { Text = "데이터 폴더 열기", AutoSize = true, MinimumSize = new Size(130, 28) };
        dataButton.Click += (_, _) => OpenDataFolder();

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        panel.Controls.Add(closeButton);
        panel.Controls.Add(dataButton);
        return panel;
    }

    /// <summary>설정·이력·로그가 저장된 폴더를 연다. 문제가 생겼을 때 로그를 찾기 쉽도록.</summary>
    private void OpenDataFolder()
    {
        string folder = AppPaths.RootDirectory;

        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "아직 데이터 폴더가 만들어지지 않았습니다.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        OpenShell(folder);
    }

    private static void OpenShell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // 메일 프로그램이 설정되지 않은 PC 등. 조용히 넘어간다.
        }
    }

    // ── 배치 보조 ────────────────────────────────────────

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 60,
        ForeColor = Color.DimGray
    };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.AddRange(controls);
        return row;
    }

    private static Control Gap(int height) => new Label { AutoSize = false, Height = height, Width = 1 };
}
