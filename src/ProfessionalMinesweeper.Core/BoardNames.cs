namespace ProfessionalMinesweeper.Core;

/// <summary>Chinese display names shared by both front ends.</summary>
public static class BoardNames
{
    public static string Display(BoardType type) => type switch
    {
        BoardType.Square => "经典方格",
        BoardType.Hexagon => "六边形",
        BoardType.HexagonShifted => "六边形（错位）",
        BoardType.OctagonSquare => "八边形方格",
        BoardType.Triangle => "三角形",
        BoardType.TriangleShifted => "三角形（错位）",
        BoardType.Parquet => "拼花地板",
        BoardType.ParquetFishbone => "鱼骨拼花",
        BoardType.Floor => "地板",
        BoardType.Wall => "砖墙",
        BoardType.CrossedSquares => "交叉方格",
        BoardType.Circles60 => "60° 圆形",
        BoardType.Circles90 => "90° 圆形",
        BoardType.Pentagonal => "五边形",
        BoardType.Mosaic57 => "5,7 镶嵌",
        BoardType.TiltedH => "斜 H",
        BoardType.RealPentagonal => "正五边形",
        BoardType.Diamond => "菱形",
        BoardType.Sunflower => "碎晶（自研）",
        BoardType.Vortex => "漩涡（自研）",
        _ => type.ToString()
    };

    public static string Difficulty(int level) => level switch
    {
        0 => "初级",
        1 => "中级",
        _ => "高级"
    };
}
