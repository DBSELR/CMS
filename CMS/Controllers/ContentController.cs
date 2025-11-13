
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using LMS.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public ContentController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        private Dictionary<string, object> ReadRow(SqlDataReader reader)
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
                row[camel] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            return row;
        }



        [HttpPost("UploadFile")]
        public async Task<IActionResult> UploadFile(
    [FromForm] IFormFile? file,                 // ← optional
    [FromForm] int courseId,
    [FromForm] string? title,
    [FromForm] string? description,
    [FromForm] string? contentType,
    [FromForm] int unitId,
    [FromForm] string? vurl)
        {
            string? fileUrl = null;
            string? storedFileName = null;
            var uploadedAtUtc = DateTime.UtcNow;

            if (file != null && file.Length > 0)
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploadsPath = Path.Combine(webRoot, "uploads", "course-content");
                Directory.CreateDirectory(uploadsPath);

                var original = Path.GetFileName(file.FileName);
                var ext = Path.GetExtension(original);
                var baseName = Path.GetFileNameWithoutExtension(original);

                // sanitize and timestamp
                baseName = System.Text.RegularExpressions.Regex.Replace(baseName, @"[^A-Za-z0-9_\-]+", "_");
                var ts = uploadedAtUtc.ToString("yyyyMMdd_HHmmssfff");
                storedFileName = $"{baseName}_{ts}{ext}";

                var filePath = Path.Combine(uploadsPath, storedFileName);
                await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                fileUrl = $"/uploads/course-content/{storedFileName}";

                // default contentType from uploaded file if not provided
                if (string.IsNullOrWhiteSpace(contentType))
                    contentType = file.ContentType;
            }

            int newId;
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_CourseContent_UploadFile", conn) { CommandType = CommandType.StoredProcedure };

            cmd.Parameters.AddWithValue("@CourseId", courseId);
            cmd.Parameters.AddWithValue("@Title", title ?? "");
            cmd.Parameters.AddWithValue("@Description", description ?? "");

            // Pass SQL NULL when no file uploaded
            var pFileUrl = cmd.Parameters.Add("@FileUrl", SqlDbType.NVarChar, 1024);
            pFileUrl.Value = (object?)fileUrl ?? DBNull.Value;

            var pContentType = cmd.Parameters.Add("@ContentType", SqlDbType.NVarChar, 256);
            pContentType.Value = (object?)contentType ?? DBNull.Value;

            cmd.Parameters.AddWithValue("@UploadedAt", uploadedAtUtc);
            cmd.Parameters.AddWithValue("@UnitId", unitId);

            var pVurl = cmd.Parameters.Add("@vurl", SqlDbType.NVarChar, 1024);
            pVurl.Value = (object?)vurl ?? DBNull.Value;

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            newId = Convert.ToInt32(result);

            return Ok(new
            {
                id = newId,
                courseId,
                title,
                description,
                fileUrl,          // null if not uploaded
                contentType,      // may be null
                uploadedAt = uploadedAtUtc,
                unitId,
                vurl,
                storedFileName
            });
        }




        // [HttpPost("UploadFile")]
        // public async Task<IActionResult> UploadFile(
        //[FromForm] IFormFile file,
        //[FromForm] int courseId,
        //[FromForm] string title,
        //[FromForm] string description,
        //[FromForm] string contentType,
        //[FromForm] int unitId,
        //[FromForm] string vurl)
        // {
        //     if (file == null || file.Length == 0)
        //         return BadRequest("File is empty.");

        //     var uploadsPath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "course-content");
        //     Directory.CreateDirectory(uploadsPath);

        //     var originalFileName = Path.GetFileName(file.FileName); // Keep exact original name
        //     var filePath = Path.Combine(uploadsPath, originalFileName);
        //     var fileUrl = $"/uploads/course-content/{originalFileName}";

        //     try
        //     {
        //         using var stream = new FileStream(filePath, FileMode.Create); // Overwrites if exists
        //         await file.CopyToAsync(stream);
        //     }
        //     catch (Exception ex)
        //     {
        //         return StatusCode(500, "File save failed: " + ex.Message);
        //     }

        //     int newId = 0;
        //     using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //     using var cmd = new SqlCommand("sp_CourseContent_UploadFile", conn)
        //     {
        //         CommandType = CommandType.StoredProcedure
        //     };
        //     cmd.Parameters.AddWithValue("@CourseId", courseId);
        //     cmd.Parameters.AddWithValue("@Title", title ?? "");
        //     cmd.Parameters.AddWithValue("@Description", description ?? "");
        //     cmd.Parameters.AddWithValue("@FileUrl", fileUrl);
        //     cmd.Parameters.AddWithValue("@ContentType", contentType ?? "");
        //     cmd.Parameters.AddWithValue("@UploadedAt", DateTime.UtcNow);
        //     cmd.Parameters.AddWithValue("@UnitId", unitId);
        //     cmd.Parameters.AddWithValue("@vurl", vurl ?? "");

        //     await conn.OpenAsync();
        //     var result = await cmd.ExecuteScalarAsync();
        //     newId = Convert.ToInt32(result);

        //     return Ok(new
        //     {
        //         id = newId,
        //         courseId,
        //         title,
        //         description,
        //         fileUrl,
        //         contentType,
        //         uploadedAt = DateTime.UtcNow,
        //         unitId,
        //         vurl
        //     });
        // }



        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_CourseContent_GetById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return Ok(ReadRow(reader));

            return NotFound();
        }

        [HttpGet("Course/{courseId}")]
        public async Task<IActionResult> GetByCourse(int courseId)
        {
            var result = new List<Dictionary<string, object>>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_CourseContent_GetByCourse", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@CourseId", courseId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(ReadRow(reader));

            return Ok(result);
        }

        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CourseContent updated)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_CourseContent_Update", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Title", updated.Title ?? "");
            cmd.Parameters.AddWithValue("@Description", updated.Description ?? "");
            cmd.Parameters.AddWithValue("@FileUrl", updated.FileUrl ?? "");
            cmd.Parameters.AddWithValue("@ContentType", updated.ContentType ?? "");
            cmd.Parameters.AddWithValue("@UploadedAt", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return NoContent();
        }

        [HttpGet("stats/{courseId}")]
        public async Task<IActionResult> GetContentStatsByCourse(int courseId)
        {
           // var result = new List<Dictionary<string, object>>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_CourseContent_GetStatsByCourse", conn)
            {
                CommandType = CommandType.StoredProcedure
            };  

            cmd.Parameters.AddWithValue("@CourseId", courseId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var result = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var camelCaseName = char.ToLowerInvariant(columnName[0]) + columnName.Substring(1);
                    result[camelCaseName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                return Ok(result);
            }

            return Ok(new Dictionary<string, object>()); // or a custom empty response
                                                         //while (await reader.ReadAsync())
                                                         //    result.Add(ReadRow(reader));

            //return Ok(result);

        }

        //if (await reader.ReadAsync())
        //{
        //    int pdfCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
        //    int videoCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        //    int ebookCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

        //    return Ok(new
        //    {
        //        pdfCount,
        //        videoCount,
        //        ebookCount
        //    });
        //}

        //return Ok(new { pdfCount = 0, videoCount = 0, ebookCount = 0 });

        //    [HttpDelete("Delete/{id}")]
        //    public async Task<IActionResult> Delete(int id)
        //    {
        //        string fileUrl = null;

        //        using (var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //        {
        //            var getCmd = new SqlCommand("sp_CourseContent_GetById", conn)
        //            {
        //                CommandType = CommandType.StoredProcedure
        //            };
        //            getCmd.Parameters.AddWithValue("@Id", id);
        //            await conn.OpenAsync();
        //            using var reader = await getCmd.ExecuteReaderAsync();
        //            if (await reader.ReadAsync())
        //                fileUrl = reader["FileUrl"].ToString();
        //            else
        //                return NotFound();
        //        }

        //        if (!string.IsNullOrWhiteSpace(fileUrl))
        //        {
        //            var fullPath = Path.Combine(_env.WebRootPath, fileUrl.TrimStart('/'));
        //            if (System.IO.File.Exists(fullPath))
        //                System.IO.File.Delete(fullPath);
        //        }

        //        using var conn2 = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //        using var cmd = new SqlCommand("sp_CourseContent_Delete", conn2)
        //        {
        //            CommandType = CommandType.StoredProcedure
        //        };
        //        cmd.Parameters.AddWithValue("@Id", id);
        //        await conn2.OpenAsync();
        //        await cmd.ExecuteNonQueryAsync();

        //        return NoContent();
        //    }


        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // 1) Look up row to get FileUrl (if any)
            string? fileUrl = null;

            await using (var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            await using (var cmd = new SqlCommand("sp_CourseContent_GetById", conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@Id", id);
                await conn.OpenAsync();

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    fileUrl = reader["FileUrl"] as string;
                }
                else
                {
                    return NotFound(); // no such row
                }
            }

            // 2) Delete DB row first (source of truth). We don’t want to keep a DB row if file deletion throws.
            await using (var conn2 = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            await using (var delCmd = new SqlCommand("sp_CourseContent_Delete", conn2) { CommandType = CommandType.StoredProcedure })
            {
                delCmd.Parameters.AddWithValue("@Id", id);
                await conn2.OpenAsync();
                await delCmd.ExecuteNonQueryAsync();
            }

            // 3) Attempt filesystem deletion if FileUrl is a LOCAL path (not an external URL like Vimeo)
            //    - Safe against null WebRootPath
            //    - Safe against path traversal
            //    - Doesn’t throw back to client; just tries and moves on.
            if (!string.IsNullOrWhiteSpace(fileUrl) && IsLikelyLocalPath(fileUrl))
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    // Normalize: trim leading '/', convert to OS separators
                    var relative = fileUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

                    // We only allow deletion inside /uploads/course-content
                    var uploadsRoot = Path.Combine(webRoot, "uploads", "course-content");
                    Directory.CreateDirectory(uploadsRoot); // ensure path exists for GetFullPath comparison

                    var fullPath = Path.GetFullPath(Path.Combine(webRoot, relative));
                    var uploadsRootFull = Path.GetFullPath(uploadsRoot);

                    // Path traversal guard: fullPath must lie under uploadsRoot
                    if (fullPath.StartsWith(uploadsRootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        if (System.IO.File.Exists(fullPath))
                        {
                            // Exclusive delete (optional: wrap in using for FileShare checks)
                            System.IO.File.Delete(fullPath);
                        }
                    }
                    // else: ignore silently; someone tampered with FileUrl or it’s outside allowed folder
                }
                catch (IOException)
                {
                    // TODO: log warning (don’t fail API): file may be locked or already deleted
                }
                catch (UnauthorizedAccessException)
                {
                    // TODO: log warning: permissions issue
                }
                // Catch nothing else; let truly unexpected errors bubble in logs, not to the client.
            }

            return NoContent();
        }

      

        private static bool IsLikelyLocalPath(string urlOrPath)
        {
            // returns true if it looks like a site-relative path like "/uploads/course-content/abc.pdf"
            // and not an absolute URL (http/https)
            if (string.IsNullOrWhiteSpace(urlOrPath)) return false;

            // reject absolute URLs
            if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var absUri) &&
                (absUri.Scheme == Uri.UriSchemeHttp || absUri.Scheme == Uri.UriSchemeHttps))
            {
                return false;
            }

            // accept absolute-path style or relative-path style
            return urlOrPath.StartsWith("/") || urlOrPath.IndexOfAny(new[] { '\\', '/' }) >= 0;
        }




    }


}
