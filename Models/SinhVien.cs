using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentManagementApp.Models
{
    public class SinhVien
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("masv")]
        public string MaSv { get; set; } = string.Empty;

        [BsonElement("hoten")]
        public string HoTen { get; set; } = string.Empty;

        [BsonElement("tuoi")]
        public int Tuoi { get; set; }

        [BsonElement("phai")]
        public string Phai { get; set; } = "Nam";

        [BsonElement("malop")]
        public string MaLop { get; set; } = string.Empty;

        [BsonElement("ngoaingu")]
        public List<string> NgoaiNgu { get; set; } = new();

        [BsonElement("monhoc")]
        public List<MonHoc> MonHoc { get; set; } = new();

        // Thuộc tính hỗ trợ hiển thị trên DataGrid UI
        [BsonIgnore]
        public string NgoaiNguHienThi => NgoaiNgu != null && NgoaiNgu.Count > 0 ? string.Join(", ", NgoaiNgu) : "(Không có)";

        [BsonIgnore]
        public int SoMonHoc => MonHoc?.Count ?? 0;

        [BsonIgnore]
        public double DiemTrungBinh => (MonHoc != null && MonHoc.Count > 0)
            ? System.Math.Round(MonHoc.Average(m => m.Diem), 2)
            : 0.0;

        [BsonIgnore]
        public string XepLoai
        {
            get
            {
                if (MonHoc == null || MonHoc.Count == 0) return "Chưa có điểm";
                double dtb = DiemTrungBinh;
                if (dtb >= 8.5) return "Xuất sắc";
                if (dtb >= 7.0) return "Giỏi";
                if (dtb >= 5.5) return "Khá";
                return "Trung bình/Yếu";
            }
        }
    }
}
