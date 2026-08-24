using MongoDB.Bson.Serialization.Attributes;

namespace StudentManagementApp.Models
{
    public class MonHoc
    {
        [BsonElement("mamon")]
        public string MaMon { get; set; } = string.Empty;

        [BsonElement("tenmon")]
        public string TenMon { get; set; } = string.Empty;

        [BsonElement("diem")]
        public double Diem { get; set; }
    }
}
