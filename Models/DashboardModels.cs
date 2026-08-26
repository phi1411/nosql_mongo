using System.Collections.Generic;

namespace StudentManagementApp.Models
{
    public class KpiDashboardDto
    {
        public long TongSinhVien { get; set; }
        public int TongSoLop { get; set; }
        public double DiemTbToanTruong { get; set; }
        public double TyLeNam { get; set; }
        public double TyLeNu { get; set; }
        public long SoLuongNam { get; set; }
        public long SoLuongNu { get; set; }
    }

    public class ThongKeLopDto
    {
        public string MaLop { get; set; } = string.Empty;
        public int SiSo { get; set; }
        public double DiemTbCaoNhat { get; set; }
        public double DiemTbThapNhat { get; set; }
    }

    public class ThongKeNgoaiNguDto
    {
        public string NgoaiNgu { get; set; } = string.Empty;
        public int SoLuongSinhVien { get; set; }
        public double TyLePhanTram { get; set; }
    }

    public class TopSinhVienDto
    {
        public int Hang { get; set; }
        public string MaSv { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public string MaLop { get; set; } = string.Empty;
        public double DiemTrungBinh { get; set; }
        public string XepLoai { get; set; } = string.Empty;
    }

    public class PhanLoaiHocLucDto
    {
        public string Loai { get; set; } = string.Empty;
        public int SoLuong { get; set; }
        public double TyLePhanTram { get; set; }
        public string MauSac { get; set; } = "#3B82F6";
    }

    public class DashboardSummary
    {
        public KpiDashboardDto Kpi { get; set; } = new();
        public List<ThongKeLopDto> ThongKeLop { get; set; } = new();
        public List<ThongKeNgoaiNguDto> ThongKeNgoaiNgu { get; set; } = new();
        public List<TopSinhVienDto> Top5SinhVien { get; set; } = new();
        public List<PhanLoaiHocLucDto> PhanLoaiHocLuc { get; set; } = new();
    }

    /// <summary>
    /// DTO chứa thông tin Mã môn và Tên môn phục vụ chức năng gợi ý (Autocomplete)
    /// </summary>
    public class MonHocSuggestionDto
    {
        public string MaMon { get; set; } = string.Empty;
        public string TenMon { get; set; } = string.Empty;
        public string DisplayText => $"{MaMon} - {TenMon}";
    }
}
