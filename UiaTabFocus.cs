using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace ClaudeNotifier;

public static class UiaTabFocus
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

    public static void Focus(ClaudeEvent evt)
    {
        try
        {
            string? shortGuid = (!string.IsNullOrEmpty(evt.WtSession) && evt.WtSession!.Length >= 8)
                ? evt.WtSession.Substring(0, 8) : null;
            string? tabTitle = evt.TabTitle;

            foreach (var proc in Process.GetProcessesByName("WindowsTerminal"))
            {
                if (proc.MainWindowHandle == IntPtr.Zero) continue;

                AutomationElement? wtRoot;
                try { wtRoot = AutomationElement.FromHandle(proc.MainWindowHandle); }
                catch { continue; }
                if (wtRoot == null) continue;

                var tabContainer = wtRoot.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab));
                if (tabContainer == null) continue;

                var tabs = tabContainer.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

                AutomationElement? match = null;

                // Strategy 1: exact tab-title match
                if (!string.IsNullOrEmpty(tabTitle))
                {
                    foreach (AutomationElement tab in tabs)
                    {
                        if (string.Equals(tab.Current.Name ?? "", tabTitle, StringComparison.Ordinal))
                        { match = tab; break; }
                    }
                }

                // Strategy 2: substring containing short GUID
                if (match == null && !string.IsNullOrEmpty(shortGuid))
                {
                    foreach (AutomationElement tab in tabs)
                    {
                        if ((tab.Current.Name ?? "").IndexOf(shortGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                        { match = tab; break; }
                    }
                }

                // Strategy 3: tab name contains cwd folder basename
                if (match == null && !string.IsNullOrEmpty(evt.Cwd))
                {
                    var baseName = System.IO.Path.GetFileName(evt.Cwd.TrimEnd('\\', '/'));
                    if (!string.IsNullOrEmpty(baseName))
                    {
                        foreach (AutomationElement tab in tabs)
                        {
                            if ((tab.Current.Name ?? "").IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0)
                            { match = tab; break; }
                        }
                    }
                }

                if (match != null)
                {
                    if (IsIconic(proc.MainWindowHandle)) ShowWindow(proc.MainWindowHandle, 9 /* SW_RESTORE */);
                    SetForegroundWindow(proc.MainWindowHandle);

                    if (match.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var sip) && sip is SelectionItemPattern sel)
                    {
                        sel.Select();
                        App.Log($"FocusTab: selected '{match.Current.Name}' in pid {proc.Id}");
                        return;
                    }
                    if (match.TryGetCurrentPattern(InvokePattern.Pattern, out var ip) && ip is InvokePattern inv)
                    {
                        inv.Invoke();
                        App.Log($"FocusTab: invoked '{match.Current.Name}' in pid {proc.Id}");
                        return;
                    }
                }
            }

            App.Log($"FocusTab: no match for title='{tabTitle}' guid='{shortGuid}' cwd='{evt.Cwd}'");
        }
        catch (Exception ex)
        {
            App.Log("FocusTab failed: " + ex.Message);
        }
    }
}
