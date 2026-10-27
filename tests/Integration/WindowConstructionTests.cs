using AgentNetDoctor.App;

namespace AgentNetDoctor.IntegrationTests;

public sealed class WindowConstructionTests
{
    [Fact]
    public void MainWindowResourcesLoadAndCatalogIsPopulatedWithoutShowingWindow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                Assert.IsType<System.Windows.Controls.ComboBox>(window.FindName("HomeToolBox"));
                Assert.True(Assert.IsType<System.Windows.Controls.Button>(window.FindName("BasicStartButton")).IsEnabled);
                Assert.False(Assert.IsType<System.Windows.Controls.Expander>(window.FindName("AdvancedTools")).IsExpanded);
                Assert.Null(window.FindName("ProbeControls"));
                var grid = Assert.IsType<System.Windows.Controls.DataGrid>(window.FindName("CatalogGrid"));
                Assert.Equal(21, grid.Items.Count);
                window.Close();
                foreach (var tool in AgentNetDoctor.Core.Adapters.ToolCatalog.All)
                {
                    var networkWindow = new ToolNetworkWindow(tool.Id);
                    var selector = Assert.IsType<System.Windows.Controls.ComboBox>(networkWindow.FindName("ToolBox"));
                    Assert.Equal(tool.Id, selector.SelectedValue);
                    Assert.False(Assert.IsType<System.Windows.Controls.Expander>(networkWindow.FindName("AdvancedOptions")).IsExpanded);
                    Assert.IsType<System.Windows.Controls.TextBlock>(networkWindow.FindName("SummaryTitle"));
                    networkWindow.Close();
                }
            }
            catch (Exception error) { failure = error; }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Window resource initialization timed out");
        Assert.Null(failure);
    }
}
