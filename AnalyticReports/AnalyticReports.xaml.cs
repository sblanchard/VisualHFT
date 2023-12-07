using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VisualHFT.AnalyticReports.ViewModel;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.AnalyticReport;

/// <summary>
///     Interaction logic for BackTestReport.xaml
/// </summary>
public partial class AnalyticReport : Window
{
    public AnalyticReport()
    {
        InitializeComponent();
    }

    public List<Position> Signals
    {
        get => originalSignal.ToList();
        set => originalSignal = value;
    }

    public List<Position> originalSignal { get; set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadMM();
        LoadData();
    }

    private void LoadMM()
    {
        //*************************************************************************************
        //LOAD MONEY MANAGEMENT systems
        //cboMoneyManagement.ItemsSource = MMBase.LoadAllMM();
        //cboMoneyManagement.DisplayMemberPath = "Key";
        //cboMoneyManagement.SelectedValuePath = "Value";
        //*************************************************************************************
    }

    private void LoadData()
    {
        if (Signals != null)
            Signals = Signals.OrderBy(x => x.CreationTimeStamp).ToList();
        if (Signals.Count > 0)
        {
            Title = "HFT Analytics";

            try
            {
                ((vmStrategyHeader)ucStrategyHeader1.DataContext).LoadData(Signals.ToList());
                ((vmOverview)ucOverview1.DataContext).LoadData(Signals.ToList());
                ((vmEquityChart)ucEquityChart1.DataContext).LoadData(Signals.ToList());
                ((vmStats)ucStats1.DataContext).LoadData(Signals.ToList());
                ((vmCharts)ucCharts1.DataContext).LoadData(Signals.ToList());
                ((vmChartsStatistics)ucChartsStatistics1.DataContext).LoadData(Signals.ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERRROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Activate();
        }
        else
        {
            MessageBox.Show("No data.");
        }
    }

    private void cboPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cboPalette.SelectedItem != null)
            LoadData();
    }

    private void mnuPrint_Click(object sender, RoutedEventArgs e)
    {
        var toPrint = scrollviewer1.Content as StackPanel;
        //ScrollViewer toPrint = scrollviewer1;
        //Window toPrint = this;

        var originalW = toPrint.ActualWidth;
        var originalH = toPrint.ActualHeight;

        var printDlg = new PrintDialog();
        printDlg.PrintTicket.PageOrientation = PageOrientation.Landscape;
        printDlg.PrintTicket.PageMediaType = PageMediaType.Continuous;
        printDlg.PrintTicket.PageMediaSize = new PageMediaSize(11 * 96, 8 * 96);
        var print = printDlg.ShowDialog();

        if (print == true)
        {
            //get selected printer capabilities
            var capabilities = printDlg.PrintQueue.GetPrintCapabilities(printDlg.PrintTicket);
            //get scale of the print wrt to screen of WPF visual
            var scale = Math.Min(printDlg.PrintTicket.PageMediaSize.Width.Value / toPrint.ActualWidth,
                printDlg.PrintTicket.PageMediaSize.Height.Value / toPrint.ActualHeight);


            //Transform the Visual to scale
            toPrint.LayoutTransform = new ScaleTransform(scale, scale);

            //get the size of the printer page
            var sz = new Size(capabilities.PageImageableArea.ExtentWidth, capabilities.PageImageableArea.ExtentHeight);
            //update the layout of the visual to the printer page size.
            toPrint.Measure(sz);
            toPrint.Arrange(new Rect(
                new Point(capabilities.PageImageableArea.OriginWidth, capabilities.PageImageableArea.OriginHeight),
                sz));

            var paginator = new ProgramPaginator(scrollviewer1.Content as StackPanel);
            paginator.PageSize = new Size(printDlg.PrintableAreaWidth, printDlg.PrintableAreaHeight);

            printDlg.PrintDocument(paginator, ucStrategyHeader1.txtStrategyName.Content.ToString());

            //update the layout of the visual to the printer page size.            
            sz = new Size(originalW, originalH);
            toPrint.Measure(sz);
            toPrint.Arrange(new Rect(
                new Point(capabilities.PageImageableArea.OriginWidth, capabilities.PageImageableArea.OriginHeight),
                sz));
        }
    }

    private void butReload_Click(object sender, RoutedEventArgs e)
    {
        //if (cboMoneyManagement.SelectedItem != null && (((KeyValuePair<string, object>)cboMoneyManagement.SelectedItem).Value as MMBase) != null )
        //    ApplyMM(((KeyValuePair<string, object>)cboMoneyManagement.SelectedItem).Value as MMBase);
        //else
        //{
        if (cboPalette.SelectedItem != null)
            LoadData();
        //}
    }

    private void cboMoneyManagement_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }
}