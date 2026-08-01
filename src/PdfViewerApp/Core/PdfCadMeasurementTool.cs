using System;
using System.Collections.Generic;
using System.Windows;

namespace PdfViewerApp.Core;

public enum CadMeasurementType
{
    Distance,
    Perimeter,
    Area
}

public enum CadUnit
{
    Millimeters,
    Centimeters,
    Meters,
    Inches
}

public class PdfCadMeasurementTool
{
    public CadMeasurementType MeasurementType { get; set; } = CadMeasurementType.Distance;
    public CadUnit Unit { get; set; } = CadUnit.Meters;
    public double ScaleFactor { get; set; } = 1.0; // 1 pixel/pt to target unit

    public static double CalculateDistance(Point p1, Point p2, double scaleFactor)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double distanceInPoints = Math.Sqrt(dx * dx + dy * dy);
        return distanceInPoints * scaleFactor;
    }

    public static double CalculatePerimeter(IList<Point> points, double scaleFactor)
    {
        if (points == null || points.Count < 2) return 0;

        double totalDistance = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            totalDistance += CalculateDistance(points[i], points[i + 1], scaleFactor);
        }

        if (points.Count > 2)
        {
            totalDistance += CalculateDistance(points[points.Count - 1], points[0], scaleFactor);
        }

        return totalDistance;
    }

    public static double CalculateArea(IList<Point> points, double scaleFactor)
    {
        if (points == null || points.Count < 3) return 0;

        double areaInPointsSquare = 0;
        int j = points.Count - 1;

        for (int i = 0; i < points.Count; i++)
        {
            areaInPointsSquare += (points[j].X + points[i].X) * (points[j].Y - points[i].Y);
            j = i;
        }

        double rawArea = Math.Abs(areaInPointsSquare / 2.0);
        return rawArea * (scaleFactor * scaleFactor);
    }

    public static string FormatMeasurementText(double value, CadMeasurementType type, CadUnit unit)
    {
        string unitSuffix = unit switch
        {
            CadUnit.Millimeters => "mm",
            CadUnit.Centimeters => "cm",
            CadUnit.Meters => "m",
            CadUnit.Inches => "in",
            _ => "m"
        };

        if (type == CadMeasurementType.Area)
        {
            unitSuffix += "²";
        }

        return $"{value:N2} {unitSuffix}";
    }
}
