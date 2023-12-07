using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml;

namespace VisualHFT.UserControls;

/// <summary>
///     Interaction logic for MetricTile.xaml
/// </summary>
public partial class MetricTile : UserControl
{
    public MetricTile()
    {
        InitializeComponent();
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox != null) ConvertHtmlToTextBlock(textBox.Text, txtToolTip);
    }

    public TextBlock ConvertHtmlToTextBlock(string htmlText, TextBlock textBlock)
    {
        textBlock.TextWrapping = TextWrapping.Wrap;
        textBlock.Width = 300;

        var doc = new XmlDocument();
        doc.LoadXml("<root>" + htmlText + "</root>");

        foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            if (node.NodeType == XmlNodeType.Text)
            {
                textBlock.Inlines.Add(new Run(node.Value));
            }
            else if (node.Name == "br")
            {
                textBlock.Inlines.Add(new LineBreak());
            }
            else if (node.Name == "b")
            {
                var run = new Run(node.InnerText);
                run.FontWeight = FontWeights.Bold;
                textBlock.Inlines.Add(run);
            }
            else if (node.Name == "i")
            {
                var run = new Run(node.InnerText);
                run.FontStyle = FontStyles.Italic;
                textBlock.Inlines.Add(run);
            }

        // Add more formatting cases as needed
        return textBlock;
    }
}