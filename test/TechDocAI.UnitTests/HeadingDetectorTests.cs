using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Chunkers;
using Xunit;

namespace TechDocAI.UnitTests;

public class HeadingDetectorTests
{
    [Fact]
    public void Analyze_UniformFontSize_ReturnsNoHeadings()
    {
        var lines = new List<ExtractedLine>
        {
            new("First line of paragraph.", 12.0, PageNumber: 1),
            new("Second line of paragraph.", 12.0, PageNumber: 1),
            new("Third line of paragraph.", 12.0, PageNumber: 1)
        };

        var result = HeadingDetector.Analyze(lines);

        Assert.False(result.HasHeadings);
    }

    [Fact]
    public void Analyze_ZeroFontSize_ReturnsNoHeadings()
    {
        var lines = new List<ExtractedLine>
        {
            new("Line 1 of plain text", 0, LineNumber: 1),
            new("Line 2 of plain text", 0, LineNumber: 2)
        };

        var result = HeadingDetector.Analyze(lines);

        Assert.False(result.HasHeadings);
    }

    [Fact]
    public void Analyze_ShortLargerLine_IdentifiesHeading()
    {
        var lines = new List<ExtractedLine>
        {
            new("1. Overview", 18.0, PageNumber: 1),
            new("This is the first sentence of body text describing the system.", 12.0, PageNumber: 1),
            new("This is the second sentence of body text describing the system.", 12.0, PageNumber: 1),
            new("This is the third sentence of body text describing the system.", 12.0, PageNumber: 1)
        };

        var result = HeadingDetector.Analyze(lines);

        Assert.True(result.HasHeadings);
        Assert.True(result.IsHeading(0));
        Assert.False(result.IsHeading(1));
        Assert.Equal("1. Overview", result.GetHeadingPath(1));
    }

    [Fact]
    public void Analyze_LongLineWithLargeFont_NotTreatedAsHeading()
    {
        var longText = new string('A', 150); // > 120 chars
        var lines = new List<ExtractedLine>
        {
            new(longText, 18.0, PageNumber: 1),
            new("Body text here.", 12.0, PageNumber: 1),
            new("More body text here.", 12.0, PageNumber: 1)
        };

        var result = HeadingDetector.Analyze(lines);

        Assert.False(result.IsHeading(0));
    }

    [Fact]
    public void Analyze_MultiLevelHierarchy_BuildsHierarchicalHeadingPath()
    {
        var lines = new List<ExtractedLine>
        {
            new("Architecture", 20.0, PageNumber: 1),       // H1
            new("Architecture overview text.", 12.0, PageNumber: 1),
            new("Storage Layer", 16.0, PageNumber: 1),     // H2
            new("Details about storage layer.", 12.0, PageNumber: 1)
        };

        var result = HeadingDetector.Analyze(lines);

        Assert.True(result.HasHeadings);
        Assert.Equal("Architecture", result.GetHeadingPath(1));
        Assert.Equal("Architecture > Storage Layer", result.GetHeadingPath(3));
    }
}
