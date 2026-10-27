using AgentNetDoctor.MacApp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace AgentNetDoctor.MacTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DoctorApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class MacWindowTests
{
    [Fact]
    public async Task SimpleWindowHasAllToolsAndCannotTestBeforeConsentAndCollection()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() =>
        {
            var window = new MainWindow();
            window.Show();
            var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
            Assert.False(window.Advanced.IsExpanded);
            var selector = Assert.Single(controls.OfType<ComboBox>());
            Assert.Equal(21, selector.ItemCount);
            Assert.Equal("Codex", selector.SelectedItem);
            var panel = Assert.IsType<StackPanel>(window.Advanced.Content);
            var test = panel.Children.OfType<Button>().Single(button => button.Name == "TestService");
            Assert.False(test.IsEnabled);
            window.Advanced.IsExpanded = true;
            window.UpdateLayout();
            var consent = window.GetVisualDescendants().OfType<CheckBox>().Single();
            consent.IsChecked = true;
            Assert.False(test.IsEnabled);
            selector.SelectedIndex = 0;
            Assert.False(consent.IsChecked);
            Assert.False(test.IsEnabled);
            Assert.True(window.Bounds.Width >= 760);
            window.Close();
        }, default);
    }
}
