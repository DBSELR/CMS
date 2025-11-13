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
        [HttpGet("{id:int}")]
        public async Task<ActionResult<TrackResponse>> GetById([FromRoute] int id)
        {
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("dbo.sp_GetTrackByid", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@TID", id);
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

    }

  
    }
