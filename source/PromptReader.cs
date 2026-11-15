using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

internal static class PromptReader
{
    private const int MaximumLength = 16000;
    private static readonly Condition AddressBar = new AndCondition(
        new OrCondition(
            new PropertyCondition(AutomationElement.AutomationIdProperty, "addressEditBox"),
            new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"),
            new PropertyCondition(AutomationElement.NameProperty, "주소 및 검색창"),
            new PropertyCondition(AutomationElement.NameProperty, "주소 및 검색 표시줄")),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

    // A null result means the prompt isn't available through the accessibility provider.
    // An empty result means the source is supported and there is no draft to analyze.
    public static ScreenLine Read(IntPtr window, Rectangle bounds)
    {
        try
        {
            int processId = ScreenReader.WindowProcessId(window);
            using (var process = Process.GetProcessById(processId))
            {
                string name = process.ProcessName.ToLowerInvariant();
                bool browser = name == "chrome" || name == "msedge";
                bool chatApp = name == "chatgpt" && IsChatGptPackage(process.MainModule.FileName);
                if (!browser && !chatApp) return null;

                var focused = AutomationElement.FocusedElement;
                if (focused == null) return null;

                var root = AutomationElement.FromHandle(window);
                if (focused.Current.ProcessId != processId && !BelongsTo(focused, root)) return null;
                return ReadEditor(root, focused, bounds, browser);
            }
        }
        catch (Exception error)
        {
            if (!(error is ElementNotAvailableException || error is InvalidOperationException
                || error is ArgumentException || error is System.ComponentModel.Win32Exception
                || error is COMException)) throw;
            return null;
        }
    }

    private static ScreenLine ReadEditor(AutomationElement root, AutomationElement focused, Rectangle bounds, bool browser)
    {
        if (browser && !IsChatGptPage(root, DocumentOf(focused))) return null;

        var composer = FindComposer(focused);
        if (composer == null) return new ScreenLine { Text = "" };

        string draft = DraftText(composer);
        if (draft == null || draft.Length > MaximumLength) return null;
        var rect = composer.Current.BoundingRectangle;
        if (rect.IsEmpty) return null;
        var area = Rectangle.Intersect(bounds, Rectangle.FromLTRB(
            (int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
            (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom)));
        if (area.IsEmpty || area.Width == 0 || area.Height == 0) return null;
        area.Offset(-bounds.Left, -bounds.Top);
        return new ScreenLine { Text = draft.Trim(), Area = area };
    }

    private static bool IsChatGptPackage(string path)
    {
        int start = path.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return false;
        var package = path.Substring(start + @"\WindowsApps\".Length).Split('\\')[0];
        return (package.StartsWith("OpenAI.ChatGPT-Desktop_", StringComparison.OrdinalIgnoreCase)
            || package.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))
            && package.EndsWith("__2p2nqsd0c76g0", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChatGptPage(AutomationElement root, AutomationElement document)
    {
        string documentUrl = ValueOf(document);
        if (!string.IsNullOrEmpty(documentUrl)) return IsChatGptUrl(documentUrl);

        // The address bar belongs to the browser UI, never to the web document.
        var address = root.FindFirst(TreeScope.Descendants, AddressBar);
        return address != null && DocumentOf(address) == null
            && IsChatGptUrl(ValueOf(address));
    }

    private static bool BelongsTo(AutomationElement element, AutomationElement root)
    {
        var walker = TreeWalker.RawViewWalker;
        for (int depth = 0; element != null && depth < 40; depth++, element = walker.GetParent(element))
            if (Automation.Compare(element, root)) return true;
        return false;
    }

    private static AutomationElement DocumentOf(AutomationElement element)
    {
        var walker = TreeWalker.ControlViewWalker;
        for (int depth = 0; element != null && depth < 30; depth++, element = walker.GetParent(element))
        {
            var type = element.Current.ControlType;
            if (type == ControlType.Document) return element;
            if (type == ControlType.Window) break;
        }
        return null;
    }

    private static bool IsChatGptUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        if (!value.Contains("://")) value = "https://" + value;
        Uri url;
        return Uri.TryCreate(value, UriKind.Absolute, out url)
            && url.Scheme == Uri.UriSchemeHttps && url.UserInfo.Length == 0 && url.IsDefaultPort
            && (url.Host == "chatgpt.com" || url.Host == "chat.openai.com");
    }

    private static AutomationElement FindComposer(AutomationElement focused)
    {
        var walker = TreeWalker.RawViewWalker;
        for (int depth = 0; focused != null && depth < 8; depth++, focused = walker.GetParent(focused))
        {
            var control = focused.Current;
            if (control.IsPassword || control.ControlType == ControlType.Window) break;
            bool textControl = control.ControlType == ControlType.Edit || control.ControlType == ControlType.Document;
            if (IsComposer(control.AutomationId, control.Name, textControl))
                return focused;
            if (control.ControlType == ControlType.Document) break;
        }
        return null;
    }

    private static bool IsComposer(string id, string name, bool editable)
    {
        return id == "prompt-textarea" || id == "mobile-composer-prompt"
            || (editable && (name == "Chat with ChatGPT" || name == "ChatGPT와 채팅"));
    }

    private static string ValueOf(AutomationElement element)
    {
        object pattern;
        return element != null && element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)
            ? ((ValuePattern)pattern).Current.Value : null;
    }

    private static string DraftText(AutomationElement composer)
    {
        object pattern;
        var value = ValueOf(composer);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        return composer.TryGetCurrentPattern(TextPattern.Pattern, out pattern)
            ? ((TextPattern)pattern).DocumentRange.GetText(MaximumLength + 1) : value;
    }
}
