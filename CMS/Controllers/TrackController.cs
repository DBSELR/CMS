using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CMS.DTOs;
using LMS.Models;
using Azure.Core;
using static QuestPDF.Helpers.Colors;
using static ExaminationController;

namespace CMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrackController : Controller
    {
        private readonly IConfiguration _configuration;
        public TrackController(IConfiguration configuration)
        {
            _configuration = configuration;
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


        [HttpPost]
        public async Task<ActionResult<TrackResponse>> Create([FromBody] CreateTrackRequest req)
        {
            if (req is null) return BadRequest("Body is required.");

            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("dbo.sp_Track_Insert", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ID", req.ID);
            cmd.Parameters.AddWithValue("@UserId", req.UserId);
            cmd.Parameters.AddWithValue("@Status", req.Status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", (object?)req.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remarks", (object?)req.Remarks ?? DBNull.Value);

            await conn.OpenAsync();

            // The proc outputs the inserted row via OUTPUT clause.
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                var res = new TrackResponse
                {
                    TID = rdr.GetInt32(0),
                    ID = rdr.GetInt32(1),
                    UserId = rdr.GetInt32(2),
                    Status = rdr.GetString(3),
                    Date = rdr.GetDateTime(4),
                    Remarks = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                };

                // 201 Created with location
                return CreatedAtAction(nameof(GetById), new { tid = res.TID }, res);
            }

            return StatusCode(500, "Insert failed unexpectedly.");
        }
        // Optional helper to fetch by id (used by CreatedAtAction)
        [HttpGet("{tid:int}")]
        public async Task<ActionResult<TrackResponse>> GetById([FromRoute] int tid)
        {
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("dbo.sp_GetTrackBytrackid", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@TID", tid);
            await conn.OpenAsync();

            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                var res = new TrackResponse
                {
                    TID = rdr.GetInt32(0),
                    ID = rdr.GetInt32(1),
                    UserId = rdr.GetInt32(2),
                    Status = rdr.GetString(3),
                    Date = rdr.GetDateTime(4),
                    Remarks = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                };
                return Ok(res);
            }
            return NotFound();
        }

        [HttpGet("GetTrackbyCollege")]
        public async Task<IActionResult> GetTrackbyCollege(int id)
        {
            var result = new List<object>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_GetTrackByid", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@TID", id);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(ReadRow(reader));

            return Ok(result);
        }

        [HttpPut("{tid:int}")]
        public async Task<ActionResult<TrackResponse>> Update([FromRoute] int tid, [FromBody] UpdateTrackRequest req)
        {
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            using var cmd = new SqlCommand("dbo.sp_Track_Update", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@TID", tid);
            cmd.Parameters.AddWithValue("@ID", (object?)req.ID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)req.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)req.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", (object?)req.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Remarks", (object?)req.Remarks ?? DBNull.Value);

            await conn.OpenAsync();

            using var rdr = await cmd.ExecuteReaderAsync();
            if (!rdr.HasRows)
            {
                // Proc returns nothing if not found
                return NotFound();
            }

            // Proc returns the updated row
            if (await rdr.ReadAsync())
            {
                var res = new TrackResponse
                {
                    TID = rdr.GetInt32(0),
                    ID = rdr.GetInt32(1),
                    UserId = rdr.GetInt32(2),
                    Status = rdr.GetString(3),
                    Date = rdr.GetDateTime(4),
                    Remarks = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                };
                return Ok(res);
            }

            return StatusCode(500, "Update failed unexpectedly.");
        }

        //[HttpPost("CreateLead")]
        //public async Task<IActionResult> CreateLead([FromBody] LeadInfoDto dto)
        //{
        //    if (dto == null)
        //    {
        //        return BadRequest("Invalid payload.");
        //    }

        //    // Basic validation (optional – you can make this stricter)
        //    if (string.IsNullOrWhiteSpace(dto.Mobile) || string.IsNullOrWhiteSpace(dto.Email))
        //    {
        //        return BadRequest("Mobile and Email are required.");
        //    }

        //    var connString = _configuration.GetConnectionString("DefaultConnection");
        //    int newLid;

        //    try
        //    {
        //        using (var conn = new SqlConnection(connString))
        //        using (var cmd = new SqlCommand("sp_LeadInfo_Insert", conn))
        //        {
        //            cmd.CommandType = CommandType.StoredProcedure;

        //            cmd.Parameters.AddWithValue("@ID", dto.ID);
        //            cmd.Parameters.AddWithValue("@UserId", dto.UserId);
        //            cmd.Parameters.AddWithValue("@Mobile", dto.Mobile);
        //            cmd.Parameters.AddWithValue("@Email", dto.Email);

        //            var outParam = new SqlParameter("@NewLID", SqlDbType.Int)
        //            {
        //                Direction = ParameterDirection.Output
        //            };
        //            cmd.Parameters.Add(outParam);

        //            await conn.OpenAsync();
        //            await cmd.ExecuteNonQueryAsync();

        //            newLid = (int)outParam.Value;
        //        }

        //        // Return created info (you can also return dto + LID)
        //        return Ok(new
        //        {
        //            LID = newLid,
        //            dto.ID,
        //            dto.UserId,
        //            dto.Mobile,
        //            dto.Email
        //        });
        //    }
        //    catch (SqlException ex)
        //    {
        //        // FK errors or constraint failures will come here
        //        return StatusCode(500, new
        //        {
        //            message = "Error inserting LeadInfo.",
        //            sqlError = ex.Message
        //        });
        //    }
        //}

        [HttpPost("CreatemultiLeads")]
        public async Task<IActionResult> CreatemultiLeads([FromBody] List<LeadInfoDto> leads)
        {
            if (leads == null || leads.Count == 0)
                return BadRequest(new { message = "No leads provided." });

            try
            {
                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    foreach (var dto in leads)
                    {
                        using (SqlCommand cmd = new SqlCommand("sp_LeadInfo_upsert", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            cmd.Parameters.AddWithValue("@LId", dto.LId);
                            cmd.Parameters.AddWithValue("@ID", dto.ID);
                            cmd.Parameters.AddWithValue("@UserId ", dto.UserId);
                            cmd.Parameters.AddWithValue("@Mobile", dto.Mobile);
                            cmd.Parameters.AddWithValue("@Email", dto.Email);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                return Ok(new { message = "Leads Inserted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database error", error = ex.Message });
            }
        }

        [HttpGet("GetLeadsById/{id}")]
        public async Task<IActionResult> GetLeadsById(int id)
        {
            List<GetLeadInfoDto> units = new List<GetLeadInfoDto>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("Sp_GetLeadsById", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@id", id);

                    await conn.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            units.Add(new GetLeadInfoDto
                            {
                                LId = reader.GetInt32(reader.GetOrdinal("LID")),
                                ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                                Mobile = reader["Mobile"] as string,
                                Email = reader["Email"] as string
                            });
                        }
                    }
                }

                return Ok(units);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching Leads Info", error = ex.Message });
            }
        }

        [HttpDelete("DeleteLead/{lid}")]
        public async Task<IActionResult> DeleteLead(int lid)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Lead_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@lid", lid);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return NoContent();
        }

    }

  
    }
