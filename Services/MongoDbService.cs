using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using StudentManagementApp.Models;

namespace StudentManagementApp.Services
{
    public class DuplicateStudentIdException : Exception
    {
        public DuplicateStudentIdException(string message) : base(message) { }
    }

    public class MongoDbService
    {
        private static MongoDbService? _instance;
        private static readonly object _lock = new();

        private readonly MongoClient _client;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<SinhVien> _sinhVienCollection;
        private readonly string _connectionString;
        private readonly string _databaseName;

        public static MongoDbService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new MongoDbService();
                    }
                }
                return _instance;
            }
        }

        private MongoDbService()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _connectionString = config["MongoDB:ConnectionString"] 
                ?? "mongodb+srv://nnp1426_db_user:<db_password>@cluster0.1ovaxuk.mongodb.net/?appName=Cluster0";
            _databaseName = config["MongoDB:DatabaseName"] ?? "qlsinhvien_db";
            string collectionName = config["MongoDB:CollectionName"] ?? "sinhvien";

            var settings = MongoClientSettings.FromConnectionString(_connectionString);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);
            settings.SocketTimeout = TimeSpan.FromSeconds(15);

            _client = new MongoClient(settings);
            _database = _client.GetDatabase(_databaseName);
            _sinhVienCollection = _database.GetCollection<SinhVien>(collectionName);
        }

        public IMongoCollection<SinhVien> Collection => _sinhVienCollection;
        public IMongoDatabase Database => _database;

        /// <summary>
        /// Khởi tạo các Index bắt buộc khi ứng dụng khởi động (Tiêu chí 6)
        /// - Unique Index: masv
        /// - Compound Index: { malop: 1, hoten: 1 }
        /// </summary>
        public async Task InitializeIndexesAsync()
        {
            try
            {
                // 1. Unique Index cho masv
                var uniqueMasvIndex = new CreateIndexModel<SinhVien>(
                    Builders<SinhVien>.IndexKeys.Ascending(s => s.MaSv),
                    new CreateIndexOptions { Unique = true, Name = "idx_unique_masv" }
                );

                // 2. Compound Index cho { malop: 1, hoten: 1 }
                var compoundClassStudentIndex = new CreateIndexModel<SinhVien>(
                    Builders<SinhVien>.IndexKeys.Ascending(s => s.MaLop).Ascending(s => s.HoTen),
                    new CreateIndexOptions { Name = "idx_compound_malop_hoten" }
                );

                await _sinhVienCollection.Indexes.CreateManyAsync(new[] { uniqueMasvIndex, compoundClassStudentIndex });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi khởi tạo Index: {ex.Message}");
            }
        }

        #region 1. CRUD Cơ bản (Tiêu chí 3)

        public async Task<List<SinhVien>> GetAllAsync()
        {
            return await _sinhVienCollection.Find(Builders<SinhVien>.Filter.Empty)
                .Sort(Builders<SinhVien>.Sort.Ascending(s => s.MaLop).Ascending(s => s.HoTen))
                .ToListAsync();
        }

        public async Task<SinhVien?> GetByMaSvAsync(string masv)
        {
            return await _sinhVienCollection.Find(s => s.MaSv.ToLower() == masv.Trim().ToLower()).FirstOrDefaultAsync();
        }

        public async Task<List<SinhVien>> GetByMaLopAsync(string malop)
        {
            if (string.IsNullOrWhiteSpace(malop) || malop == "Tất cả")
            {
                return await GetAllAsync();
            }
            return await _sinhVienCollection.Find(s => s.MaLop.ToLower() == malop.Trim().ToLower())
                .Sort(Builders<SinhVien>.Sort.Ascending(s => s.HoTen))
                .ToListAsync();
        }

        public async Task<List<string>> GetAllClassesAsync()
        {
            var classes = await _sinhVienCollection.Distinct(s => s.MaLop, Builders<SinhVien>.Filter.Empty).ToListAsync();
            return classes.Where(c => !string.IsNullOrWhiteSpace(c)).OrderBy(c => c).ToList();
        }

        public async Task InsertOneAsync(SinhVien sv)
        {
            try
            {
                await _sinhVienCollection.InsertOneAsync(sv);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey || ex.WriteError.Code == 11000)
            {
                throw new DuplicateStudentIdException($"Mã sinh viên '{sv.MaSv}' đã tồn tại trong cơ sở dữ liệu!");
            }
            catch (MongoBulkWriteException ex)
            {
                if (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey || e.Code == 11000))
                {
                    throw new DuplicateStudentIdException($"Mã sinh viên '{sv.MaSv}' đã tồn tại trong cơ sở dữ liệu!");
                }
                throw;
            }
        }

        public async Task<bool> UpdateBasicInfoAsync(string masv, string hoTen, int tuoi, string phai, string maLop)
        {
            var filter = Builders<SinhVien>.Filter.Eq(s => s.MaSv, masv);
            var update = Builders<SinhVien>.Update
                .Set(s => s.HoTen, hoTen)
                .Set(s => s.Tuoi, tuoi)
                .Set(s => s.Phai, phai)
                .Set(s => s.MaLop, maLop);

            var result = await _sinhVienCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteOneAsync(string masv)
        {
            var filter = Builders<SinhVien>.Filter.Eq(s => s.MaSv, masv);
            var result = await _sinhVienCollection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }

        public async Task<long> DeleteManyByMaLopAsync(string malop)
        {
            var filter = Builders<SinhVien>.Filter.Eq(s => s.MaLop, malop);
            var result = await _sinhVienCollection.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        #endregion

        #region 2. Xử lý Mảng Nâng cao & Thay thế Document (Tiêu chí 4)

        /// <summary>
        /// Thêm ngoại ngữ mới cho sinh viên bằng toán tử $addToSet (tránh trùng) hoặc $push
        /// </summary>
        public async Task<bool> AddNgoaiNguAsync(string masv, string ngoaiNgu)
        {
            var filter = Builders<SinhVien>.Filter.Eq(s => s.MaSv, masv);
            var update = Builders<SinhVien>.Update.AddToSet(s => s.NgoaiNgu, ngoaiNgu.Trim());
            var result = await _sinhVienCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        /// <summary>
        /// Thêm môn học mới cho sinh viên bằng toán tử $push
        /// </summary>
        public async Task<bool> AddMonHocAsync(string masv, MonHoc monHoc)
        {
            var filter = Builders<SinhVien>.Filter.Eq(s => s.MaSv, masv);
            var update = Builders<SinhVien>.Update.Push(s => s.MonHoc, monHoc);
            var result = await _sinhVienCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        /// <summary>
        /// Cập nhật điểm số của một môn học cụ thể dựa vào masv và mamon bằng Positional Operator ($)
        /// Câu lệnh MongoDB: db.sinhvien.updateOne({ masv: "...", "monhoc.mamon": "..." }, { $set: { "monhoc.$.diem": diemMoi } })
        /// </summary>
        public async Task<bool> UpdateDiemMonHocAsync(string masv, string maMon, double diemMoi)
        {
            var filter = Builders<SinhVien>.Filter.And(
                Builders<SinhVien>.Filter.Eq(s => s.MaSv, masv),
                Builders<SinhVien>.Filter.Eq("monhoc.mamon", maMon.Trim().ToLower())
            );

            var update = Builders<SinhVien>.Update.Set("monhoc.$.diem", diemMoi);
            var result = await _sinhVienCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        /// <summary>
        /// Thay thế toàn bộ document sinh viên theo trường _id (replaceOne)
        /// </summary>
        public async Task<bool> ReplaceOneAsync(string id, SinhVien sv)
        {
            try
            {
                var filter = Builders<SinhVien>.Filter.Eq(s => s.Id, id);
                var result = await _sinhVienCollection.ReplaceOneAsync(filter, sv);
                return result.ModifiedCount > 0;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey || ex.WriteError.Code == 11000)
            {
                throw new DuplicateStudentIdException($"Mã sinh viên '{sv.MaSv}' bị trùng với sinh viên khác trong CSDL!");
            }
        }

        #endregion

        #region 3. Module Dashboard & Aggregation Pipeline (Tiêu chí 5)

        public async Task<DashboardSummary> GetDashboardDataAsync()
        {
            var summary = new DashboardSummary();

            // 1. KPI Cards
            long totalStudents = await _sinhVienCollection.CountDocumentsAsync(Builders<SinhVien>.Filter.Empty);
            var classList = await GetAllClassesAsync();
            int totalClasses = classList.Count;

            // Pipeline tính Điểm trung bình toàn trường (tất cả môn học của toàn bộ sinh viên)
            var gpaPipeline = new BsonDocument[]
            {
                new("$unwind", "$monhoc"),
                new("$group", new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "avgGpa", new BsonDocument("$avg", "$monhoc.diem") }
                })
            };

            double schoolGpa = 0.0;
            var gpaResult = await _sinhVienCollection.Aggregate<BsonDocument>(gpaPipeline).FirstOrDefaultAsync();
            if (gpaResult != null && gpaResult.Contains("avgGpa") && !gpaResult["avgGpa"].IsBsonNull)
            {
                schoolGpa = Math.Round(gpaResult["avgGpa"].ToDouble(), 2);
            }

            // Pipeline tính Tỷ lệ Nam / Nữ
            var genderPipeline = new BsonDocument[]
            {
                new("$group", new BsonDocument
                {
                    { "_id", "$phai" },
                    { "count", new BsonDocument("$sum", 1) }
                })
            };

            long countNam = 0;
            long countNu = 0;
            var genderResults = await _sinhVienCollection.Aggregate<BsonDocument>(genderPipeline).ToListAsync();
            foreach (var doc in genderResults)
            {
                string phai = doc["_id"].AsString;
                long count = doc["count"].ToInt64();
                if (string.Equals(phai, "Nam", StringComparison.OrdinalIgnoreCase))
                    countNam = count;
                else if (string.Equals(phai, "Nữ", StringComparison.OrdinalIgnoreCase) || string.Equals(phai, "Nu", StringComparison.OrdinalIgnoreCase))
                    countNu = count;
            }

            summary.Kpi = new KpiDashboardDto
            {
                TongSinhVien = totalStudents,
                TongSoLop = totalClasses,
                DiemTbToanTruong = schoolGpa,
                SoLuongNam = countNam,
                SoLuongNu = countNu,
                TyLeNam = totalStudents > 0 ? Math.Round((double)countNam / totalStudents * 100, 1) : 0,
                TyLeNu = totalStudents > 0 ? Math.Round((double)countNu / totalStudents * 100, 1) : 0
            };

            // 2. Thống kê theo lớp ($group theo malop, tính sĩ số, max GPA, min GPA)
            var classPipeline = new BsonDocument[]
            {
                new("$project", new BsonDocument
                {
                    { "malop", 1 },
                    { "dtb", new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gt", new BsonArray { new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$monhoc", new BsonArray() })), 0 }),
                            new BsonDocument("$avg", "$monhoc.diem"),
                            BsonNull.Value
                        })
                    }
                }),
                new("$group", new BsonDocument
                {
                    { "_id", "$malop" },
                    { "siSo", new BsonDocument("$sum", 1) },
                    { "maxGpa", new BsonDocument("$max", "$dtb") },
                    { "minGpa", new BsonDocument("$min", "$dtb") }
                }),
                new("$sort", new BsonDocument("_id", 1))
            };

            var classDocs = await _sinhVienCollection.Aggregate<BsonDocument>(classPipeline).ToListAsync();
            foreach (var doc in classDocs)
            {
                string maLop = doc["_id"].IsBsonNull ? "Chưa phân lớp" : doc["_id"].AsString;
                int siSo = doc["siSo"].ToInt32();
                double maxGpa = doc["maxGpa"].IsBsonNull ? 0.0 : Math.Round(doc["maxGpa"].ToDouble(), 2);
                double minGpa = doc["minGpa"].IsBsonNull ? 0.0 : Math.Round(doc["minGpa"].ToDouble(), 2);

                summary.ThongKeLop.Add(new ThongKeLopDto
                {
                    MaLop = maLop,
                    SiSo = siSo,
                    DiemTbCaoNhat = maxGpa,
                    DiemTbThapNhat = minGpa
                });
            }

            // 3. Thống kê độ phổ biến Ngoại ngữ ($unwind, $group, $sort giảm dần)
            var langPipeline = new BsonDocument[]
            {
                new("$unwind", "$ngoaingu"),
                new("$group", new BsonDocument
                {
                    { "_id", "$ngoaingu" },
                    { "count", new BsonDocument("$sum", 1) }
                }),
                new("$sort", new BsonDocument("count", -1))
            };

            var langDocs = await _sinhVienCollection.Aggregate<BsonDocument>(langPipeline).ToListAsync();
            long totalLangEnrollments = langDocs.Sum(d => d["count"].ToInt64());

            foreach (var doc in langDocs)
            {
                string lang = doc["_id"].AsString;
                int count = doc["count"].ToInt32();
                double pct = totalLangEnrollments > 0 ? Math.Round((double)count / totalLangEnrollments * 100, 1) : 0;

                summary.ThongKeNgoaiNgu.Add(new ThongKeNgoaiNguDto
                {
                    NgoaiNgu = lang,
                    SoLuongSinhVien = count,
                    TyLePhanTram = pct
                });
            }

            // 4. Bảng Xếp hạng Top 5 Sinh viên ($sort theo Điểm TB desc, $limit 5)
            var topPipeline = new BsonDocument[]
            {
                new("$project", new BsonDocument
                {
                    { "masv", 1 },
                    { "hoten", 1 },
                    { "malop", 1 },
                    { "dtb", new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gt", new BsonArray { new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$monhoc", new BsonArray() })), 0 }),
                            new BsonDocument("$avg", "$monhoc.diem"),
                            0.0
                        })
                    }
                }),
                new("$sort", new BsonDocument("dtb", -1)),
                new("$limit", 5)
            };

            var topDocs = await _sinhVienCollection.Aggregate<BsonDocument>(topPipeline).ToListAsync();
            int rank = 1;
            foreach (var doc in topDocs)
            {
                double dtb = Math.Round(doc["dtb"].ToDouble(), 2);
                string xepLoai = dtb >= 8.5 ? "Xuất sắc" : (dtb >= 7.0 ? "Giỏi" : (dtb >= 5.5 ? "Khá" : "Trung bình/Yếu"));
                summary.Top5SinhVien.Add(new TopSinhVienDto
                {
                    Hang = rank++,
                    MaSv = doc["masv"].AsString,
                    HoTen = doc["hoten"].AsString,
                    MaLop = doc["malop"].AsString,
                    DiemTrungBinh = dtb,
                    XepLoai = xepLoai
                });
            }

            // 5. Phân loại học lực toàn trường: Xuất sắc (>= 8.5), Giỏi (7.0 - <8.5), Khá (5.5 - <7.0), TB/Yếu (<5.5)
            var allStudents = await GetAllAsync();
            int xuatSac = 0, gioi = 0, kha = 0, tbYeu = 0;
            foreach (var sv in allStudents)
            {
                if (sv.MonHoc == null || sv.MonHoc.Count == 0) continue;
                double dtb = sv.DiemTrungBinh;
                if (dtb >= 8.5) xuatSac++;
                else if (dtb >= 7.0) gioi++;
                else if (dtb >= 5.5) kha++;
                else tbYeu++;
            }

            int gradedCount = xuatSac + gioi + kha + tbYeu;
            summary.PhanLoaiHocLuc = new List<PhanLoaiHocLucDto>
            {
                new() { Loai = "Xuất sắc (≥ 8.5)", SoLuong = xuatSac, MauSac = "#10B981", TyLePhanTram = gradedCount > 0 ? Math.Round((double)xuatSac / gradedCount * 100, 1) : 0 },
                new() { Loai = "Giỏi (7.0 - < 8.5)", SoLuong = gioi, MauSac = "#3B82F6", TyLePhanTram = gradedCount > 0 ? Math.Round((double)gioi / gradedCount * 100, 1) : 0 },
                new() { Loai = "Khá (5.5 - < 7.0)", SoLuong = kha, MauSac = "#F59E0B", TyLePhanTram = gradedCount > 0 ? Math.Round((double)kha / gradedCount * 100, 1) : 0 },
                new() { Loai = "Trung bình/Yếu (< 5.5)", SoLuong = tbYeu, MauSac = "#EF4444", TyLePhanTram = gradedCount > 0 ? Math.Round((double)tbYeu / gradedCount * 100, 1) : 0 }
            };

            return summary;
        }

        #endregion

        #region 4. Seed Data

        public async Task<int> SeedSampleDataAsync(string filePath)
        {
            if (!File.Exists(filePath)) return 0;

            string json = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<SinhVien>>(json, options);

            if (list == null || list.Count == 0) return 0;

            // Xóa sạch collection trước khi nạp dữ liệu mẫu mới
            await _sinhVienCollection.DeleteManyAsync(Builders<SinhVien>.Filter.Empty);
            await _sinhVienCollection.InsertManyAsync(list);

            return list.Count;
        }

        #endregion
    }
}
