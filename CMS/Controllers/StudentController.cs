// File: Controllers/StudentController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using LMS.DTOs;
using System;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using LMS.Models;

namespace LMS.Controllers
{
    [Route("api/student")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IFeeService _feeService;

        public StudentController(IConfiguration configuration, IFeeService feeService)
        {
            _configuration = configuration;
            _feeService = feeService;
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


        [HttpGet("students/{instructorId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetStudents(int instructorId)
        {
            var list = new List<object>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Student_GetStudents", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@InstructorId", instructorId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(ReadRow(reader));
            return Ok(list);
        }

        [HttpGet("studentcount")]
        public async Task<IActionResult> studentcount()
        {
            var list = new List<object>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_mentor_studentcount", conn) 
            { CommandType = CommandType.StoredProcedure };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(ReadRow(reader));
            return Ok(list);
        }

        [HttpGet("mentorslist")]
        public async Task<IActionResult> mentorslist()
        {
            var list = new List<object>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_mentorlist", conn)
            { CommandType = CommandType.StoredProcedure };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(ReadRow(reader));
            return Ok(list);
        }

        //[HttpPost("assign-mentors")]
        //public async Task<IActionResult> AssignMentors([FromBody] MentorAssignmentRequest request)
        //{
        //    var count = request.StudentCount;
        //    var studentsPerMentor = count / request.MentorIds.Count;
        //    int remaining = count % request.MentorIds.Count;

        //    using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //    await conn.OpenAsync();

        //    for (int i = 0; i < request.MentorIds.Count; i++)
        //    {
        //        int assignCount = studentsPerMentor + (i < remaining ? 1 : 0); // distribute remaining students

        //        using var cmd = new SqlCommand("sp_Mentor_Assign", conn)
        //        {
        //            CommandType = CommandType.StoredProcedure
        //        };
        //        cmd.Parameters.AddWithValue("@Batch", request.Batch);
        //        cmd.Parameters.AddWithValue("@ProgrammeId", request.ProgrammeId);
        //        cmd.Parameters.AddWithValue("@GroupId", request.GroupId);
        //        cmd.Parameters.AddWithValue("@sem", request.Semester);
        //        cmd.Parameters.AddWithValue("@mentorid", request.MentorIds[i]);

        //        for (int j = 0; j < assignCount; j++)
        //        {
        //            await cmd.ExecuteNonQueryAsync();
        //        }
        //    }

        //    return Ok(new { success = true, message = "Mentors assigned successfully" });
        //}

        [HttpPost("assign-mentors")]
        public async Task<IActionResult> AssignMentors([FromBody] MentorAssignmentRequest request)
        {
            if (request.Students == null || request.Students.Count == 0 || request.MentorIds == null || request.MentorIds.Count == 0)
            {
                return BadRequest("Invalid request data.");
            }

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // Loop through each selected student group
            foreach (var student in request.Students)
            {
                var count = student.StudentCount;
                var studentsPerMentor = count / request.MentorIds.Count;
                int remaining = count % request.MentorIds.Count;

                for (int i = 0; i < request.MentorIds.Count; i++)
                {
                    int assignCount = studentsPerMentor + (i < remaining ? 1 : 0);

                    using var cmd = new SqlCommand("sp_Mentor_Assign", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmd.Parameters.AddWithValue("@Batch", student.Batch);
                    cmd.Parameters.AddWithValue("@ProgrammeId", student.ProgrammeId);
                    cmd.Parameters.AddWithValue("@GroupId", student.GroupId);
                    cmd.Parameters.AddWithValue("@sem", student.Semester);
                    cmd.Parameters.AddWithValue("@mentorid", request.MentorIds[i]);

                    // Call SP for each mentor assignment
                    for (int j = 0; j < assignCount; j++)
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return Ok(new { success = true, message = "Mentors assigned successfully." });
        }



        [HttpPost("delete-mentor-assign")]
        public async Task<IActionResult> DeleteMentorAssign([FromBody] MentorAssignDeleteModel model)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Mentor_Delete", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Batch", model.BatchName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ProgrammeId", model.ProgrammeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GroupId", model.GroupId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sem", model.Semester ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@mentorid", model.MentorId ?? (object)DBNull.Value);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Deleted successfully." });
        }

        [HttpGet("studentformentors")]
        public async Task<IActionResult> studentformentors()
        {
            var list = new List<object>();
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Mentor_studentsformentor", conn)
            { CommandType = CommandType.StoredProcedure };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(ReadRow(reader));
            return Ok(list);
        }

        //[HttpPost("register")]
        //public async Task<IActionResult> Register([FromBody] StudentRegisterDto request)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    try
        //    {
        //        using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        //        // Step 1: Call SP to create user and get username
        //        using var cmd = new SqlCommand("sp_Student_Register", conn) { CommandType = CommandType.StoredProcedure };

        //        var usernameParam = new SqlParameter("@Username", SqlDbType.VarChar, 7)
        //        {
        //            Direction = ParameterDirection.Output
        //        };

        //        cmd.Parameters.AddWithValue("@Email", request.Email);
        //        cmd.Parameters.AddWithValue("@PasswordHash", "TEMP");
        //        cmd.Parameters.AddWithValue("@FirstName", request.FirstName);
        //        cmd.Parameters.AddWithValue("@LastName", request.LastName);
        //        cmd.Parameters.AddWithValue("@PhoneNumber", request.PhoneNumber);
        //        cmd.Parameters.AddWithValue("@Gender", request.Gender);
        //        cmd.Parameters.AddWithValue("@DateOfBirth", request.DateOfBirth);
        //        cmd.Parameters.AddWithValue("@ProfilePhotoUrl", request.ProfilePhotoUrl);
        //        cmd.Parameters.AddWithValue("@Address", request.Address);
        //        cmd.Parameters.AddWithValue("@City", request.City);
        //        cmd.Parameters.AddWithValue("@State", request.State);
        //        cmd.Parameters.AddWithValue("@Country", request.Country);
        //        cmd.Parameters.AddWithValue("@ZipCode", request.ZipCode);
        //        cmd.Parameters.AddWithValue("@BatchName", request.Batch);
        //        cmd.Parameters.AddWithValue("@ProgrammeId", request.programmeId);
        //        cmd.Parameters.AddWithValue("@GroupId", request.groupId);
        //        cmd.Parameters.AddWithValue("@Jsem", request.semester);
        //        cmd.Parameters.AddWithValue("@ssem", request.semester);
        //        cmd.Parameters.Add(usernameParam);

        //        await conn.OpenAsync();
        //        await cmd.ExecuteNonQueryAsync();

        //        var generatedUsername = usernameParam.Value?.ToString();
        //        if (string.IsNullOrEmpty(generatedUsername))
        //            return StatusCode(500, "Username generation failed.");

        //        // Step 2: Use username as password
        //        var rawPassword = generatedUsername;
        //        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword);

        //        // Step 3: Update password
        //        using var updateCmd = new SqlCommand("UPDATE Users SET PasswordHash = @PasswordHash WHERE Username = @Username", conn);
        //        updateCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
        //        updateCmd.Parameters.AddWithValue("@Username", generatedUsername);
        //        await updateCmd.ExecuteNonQueryAsync();

        //        // Step 4: Get newly created UserId
        //        using var userIdCmd = new SqlCommand("SELECT UserId FROM Users WHERE Username = @Username", conn);
        //        userIdCmd.Parameters.AddWithValue("@Username", generatedUsername);
        //        var userIdObj = await userIdCmd.ExecuteScalarAsync();
        //        if (userIdObj == null)
        //            return StatusCode(500, "Failed to retrieve UserId.");
        //        int userId = Convert.ToInt32(userIdObj);

        //        //// Step 5: Auto-assign courses based on Programme and Semester
        //        //using var courseCmd = new SqlCommand("SELECT distinct s1.examinationid courseid, s1.semester sem FROM SubjectBank s1 inner join SubjectAssignments s2 on " +
        //        //    " s1.examinationid = s2.examinationid and s1.BatchName = s2.BatchName and s1.semester = s2.semester  " +
        //        //    " WHERE s2.CourseId = @ProgrammeId and s2.GroupId = @GroupId and s2.BatchName=@BatchName and s2.semester=@ssem", conn);

        //        //courseCmd.Parameters.AddWithValue("@ProgrammeId", request.programmeId);
        //        //courseCmd.Parameters.AddWithValue("@GroupId", request.groupId);
        //        //courseCmd.Parameters.AddWithValue("@BatchName", request.Batch);
        //        //courseCmd.Parameters.AddWithValue("@ssem", request.semester);
        //        //using var reader = await courseCmd.ExecuteReaderAsync();

        //        //int semesterValue = request.semester;
        //        //var matchedCourseIds = new List<int>();

        //        //while (await reader.ReadAsync())
        //        //{
        //        //    if (int.TryParse(reader["sem"]?.ToString(), out int courseSemester) &&
        //        //        courseSemester == semesterValue &&
        //        //        int.TryParse(reader["courseid"]?.ToString(), out int cid))
        //        //    {
        //        //        matchedCourseIds.Add(cid);
        //        //    }
        //        //}
        //        //reader.Close();

        //        //foreach (var courseId in matchedCourseIds)
        //        //{
        //        //    using var assignCmd = new SqlCommand(@"INSERT INTO StudentCourses (UserId, CourseId, CompletionStatus, Grade, DateAssigned)
        //        //                                   VALUES (@UserId, @CourseId, 'NotStarted', 'N/A', @Now)", conn);
        //        //    assignCmd.Parameters.AddWithValue("@UserId", userId);
        //        //    assignCmd.Parameters.AddWithValue("@CourseId", courseId);
        //        //    assignCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
        //        //    await assignCmd.ExecuteNonQueryAsync();
        //        //}

        //        //// Step 6: Generate semester fee
        //        //await _feeService.GenerateSemesterFeeForCurrentSemester(userId, request.Batch, request.programmeId, request.groupId, request.semester);

        //        return Ok(new
        //        {
        //            Username = generatedUsername,
        //            Password = rawPassword,
        //            Message = "Student registered successfully"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Internal server error: {ex.Message}");
        //    }
        //}

        //[HttpPost("register")]
        //public async Task<IActionResult> Register([FromBody] StudentRegisterDto request)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    try
        //    {
        //        await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //        await using var cmd = new SqlCommand("sp_Student_Register", conn) { CommandType = CommandType.StoredProcedure };

        //        var usernameParam = new SqlParameter("@Username", SqlDbType.NVarChar, 7) { Direction = ParameterDirection.Output };
        //        cmd.Parameters.AddWithValue("@Email", request.Email ?? string.Empty);
        //        cmd.Parameters.AddWithValue("@PasswordHash", "TEMP");
        //        cmd.Parameters.AddWithValue("@FirstName", request.FirstName ?? string.Empty);
        //        cmd.Parameters.AddWithValue("@LastName", request.LastName ?? string.Empty);
        //        cmd.Parameters.AddWithValue("@PhoneNumber", request.PhoneNumber ?? string.Empty);
        //        cmd.Parameters.AddWithValue("@Gender", request.Gender ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@DateOfBirth", (object?)request.DateOfBirth ?? DBNull.Value);
        //        cmd.Parameters.AddWithValue("@ProfilePhotoUrl", request.ProfilePhotoUrl ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@Address", request.Address ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@City", request.City ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@State", request.State ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@Country", request.Country ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@ZipCode", request.ZipCode ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@BatchName", request.Batch ?? (object)DBNull.Value);
        //        cmd.Parameters.AddWithValue("@ProgrammeId", request.programmeId);
        //        cmd.Parameters.AddWithValue("@GroupId", request.groupId);
        //        cmd.Parameters.AddWithValue("@Jsem", request.semester);
        //        cmd.Parameters.AddWithValue("@ssem", request.semester);
        //        cmd.Parameters.AddWithValue("@RefCode", request.RefCode);
        //        cmd.Parameters.Add(usernameParam);

        //        await conn.OpenAsync();

        //        // Read resultset to detect conflicts/success
        //        using var reader = await cmd.ExecuteReaderAsync();
        //        var conflicts = new List<object>();
        //        bool gotAnyRows = false;
        //        bool success = false;
        //        string? generatedUsernameFromRow = null;

        //        while (await reader.ReadAsync())
        //        {
        //            gotAnyRows = true;
        //            success = reader.GetBoolean(reader.GetOrdinal("Success"));

        //            if (!success)
        //            {
        //                var typeOrdinal = reader.GetOrdinal("ConflictType");
        //                var detailsOrdinal = reader.GetOrdinal("Details");
        //                var conflictType = reader.IsDBNull(typeOrdinal) ? null : reader.GetString(typeOrdinal);
        //                var details = reader.IsDBNull(detailsOrdinal) ? null : reader.GetString(detailsOrdinal);

        //                if (!string.IsNullOrEmpty(conflictType))
        //                    conflicts.Add(new { ConflictType = conflictType, Details = details });
        //            }
        //            else
        //            {
        //                var detailsOrdinal = reader.GetOrdinal("Details");
        //                generatedUsernameFromRow = reader.IsDBNull(detailsOrdinal) ? null : reader.GetString(detailsOrdinal);
        //            }
        //        }


        //        if (gotAnyRows && !success)
        //        {
        //            return Conflict(new
        //            {
        //                error = "Duplicate fields found",
        //                conflicts
        //                // e.g. [
        //                //   { ConflictType: "EMAIL_EXISTS", Details: "Ram@dbs.com" },
        //                //   { ConflictType: "PHONE_EXISTS", Details: "903010..." },
        //                //   { ConflictType: "NAME_PAIR_EXISTS", Details: "Ram DBS" }
        //                // ]
        //            });
        //        }


        //        if (string.IsNullOrEmpty(generatedUsernameFromRow))
        //            generatedUsernameFromRow = usernameParam.Value?.ToString();

        //        if (string.IsNullOrEmpty(generatedUsernameFromRow))
        //            return StatusCode(500, "Username generation failed.");

        //        // Success: first-time password = username (hash and store)
        //        var rawPassword = generatedUsernameFromRow;
        //        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword);

        //        using (var updateCmd = new SqlCommand("UPDATE Users SET PasswordHash = @PasswordHash WHERE Username = @Username", conn))
        //        {
        //            updateCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
        //            updateCmd.Parameters.AddWithValue("@Username", generatedUsernameFromRow);
        //            await updateCmd.ExecuteNonQueryAsync();
        //        }


        //        // using var userIdCmd = new SqlCommand("SELECT UserId FROM Users WHERE Username = @Username", conn);
        //        // userIdCmd.Parameters.AddWithValue("@Username", generatedUsernameFromRow);
        //        // var userIdObj = await userIdCmd.ExecuteScalarAsync();

        //        return Ok(new
        //        {
        //            Username = generatedUsernameFromRow,
        //            Password = rawPassword,
        //            Message = "Student registered successfully"
        //        });
        //    }
        //    catch (SqlException ex)
        //    {
        //        // If you add DB unique indexes, handle those too
        //        if (ex.Number == 2627 || ex.Number == 2601)
        //        {
        //            return Conflict(new
        //            {
        //                error = "Duplicate detected by database index.",
        //                sqlError = ex.Message
        //            });
        //        }
        //        return StatusCode(500, new { error = $"SQL error {ex.Number}: {ex.Message}" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { error = $"Unexpected error: {ex.Message}" });
        //    }
        //}

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] StudentRegisterDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { error = "Username is required." });

            try
            {
                var connStr = _configuration.GetConnectionString("DefaultConnection");

                // Initial password = username; store bcrypt hash
                var rawPassword = request.Username.Trim();
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword);

                await using var conn = new SqlConnection(connStr);
                await using var cmd = new SqlCommand("sp_Student_Register", conn)
                { CommandType = CommandType.StoredProcedure };

                cmd.Parameters.AddWithValue("@Username", request.Username?.Trim() ?? string.Empty);
                cmd.Parameters.AddWithValue("@Email", request.Email ?? string.Empty);
                cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                cmd.Parameters.AddWithValue("@FirstName", request.FirstName ?? string.Empty);
                cmd.Parameters.AddWithValue("@LastName", request.LastName ?? string.Empty);
                cmd.Parameters.AddWithValue("@PhoneNumber", request.PhoneNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@Gender", (object?)request.Gender ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DateOfBirth", (object?)request.DateOfBirth ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ProfilePhotoUrl", (object?)request.ProfilePhotoUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Address", (object?)request.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", (object?)request.City ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@State", (object?)request.State ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Country", (object?)request.Country ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ZipCode", (object?)request.ZipCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BatchName", (object?)request.Batch ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ProgrammeId", request.programmeId);
               // cmd.Parameters.AddWithValue("@GroupId", request.groupId);
                cmd.Parameters.AddWithValue("@Jsem", request.semester);
                cmd.Parameters.AddWithValue("@ssem", request.semester);
                cmd.Parameters.AddWithValue("@RefCode", request.RefCode);
                cmd.Parameters.AddWithValue("@degree", (object?)request.degree ?? DBNull.Value);
                

                await conn.OpenAsync();

                // Read the proc's resultset: either conflicts (Success=0 rows) or one success row (Success=1)
                using var reader = await cmd.ExecuteReaderAsync();
                var conflicts = new List<object>();
                bool gotAnyRows = false;
                bool success = false;

                while (await reader.ReadAsync())
                {
                    gotAnyRows = true;
                    success = reader.GetBoolean(reader.GetOrdinal("Success"));

                    if (!success)
                    {
                        var ctOrd = reader.GetOrdinal("ConflictType");
                        var dtOrd = reader.GetOrdinal("Details");
                        var conflictType = reader.IsDBNull(ctOrd) ? null : reader.GetString(ctOrd);
                        var details = reader.IsDBNull(dtOrd) ? null : reader.GetString(dtOrd);
                        conflicts.Add(new { ConflictType = conflictType, Details = details });
                    }
                }

                if (gotAnyRows && !success)
                {
                    return Conflict(new
                    {
                        error = "Duplicate fields found",
                        conflicts
                    });
                }

                if (!gotAnyRows)
                    return StatusCode(500, new { error = "Registration failed with no response." });

                // Success
                return Ok(new
                {
                    Username = request.Username,
                    Password = rawPassword, // first-time password
                    Message = "Student registered successfully"
                });
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return Conflict(new
                {
                    error = "Duplicate detected by database index.",
                    sqlError = ex.Message
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"SQL error {ex.Number}: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error: {ex.Message}" });
            }
        }


        [HttpPost("Landingregister")]
        [AllowAnonymous]
        public async Task<IActionResult> LandingRegister([FromBody] StudentRegisterDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await using var cmd = new SqlCommand("sp_Student_Register", conn) { CommandType = CommandType.StoredProcedure };

                var usernameParam = new SqlParameter("@Username", SqlDbType.NVarChar, 7) { Direction = ParameterDirection.Output };
                cmd.Parameters.AddWithValue("@Email", request.Email ?? string.Empty);
                cmd.Parameters.AddWithValue("@PasswordHash", "TEMP");
                cmd.Parameters.AddWithValue("@FirstName", request.FirstName ?? string.Empty);
                cmd.Parameters.AddWithValue("@LastName", request.LastName ?? string.Empty);
                cmd.Parameters.AddWithValue("@PhoneNumber", request.PhoneNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@Gender", request.Gender ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateOfBirth", (object?)request.DateOfBirth ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ProfilePhotoUrl", request.ProfilePhotoUrl ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Address", request.Address ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@City", request.City ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@State", request.State ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Country", request.Country ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ZipCode", request.ZipCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@BatchName", request.Batch ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ProgrammeId", request.programmeId);
               // cmd.Parameters.AddWithValue("@GroupId", request.groupId);
                cmd.Parameters.AddWithValue("@Jsem", request.semester); 
                cmd.Parameters.AddWithValue("@ssem", request.semester);
                cmd.Parameters.AddWithValue("@RefCode", request.RefCode);
                cmd.Parameters.Add(usernameParam);

                await conn.OpenAsync();

                // Read resultset to detect conflicts/success
                using var reader = await cmd.ExecuteReaderAsync();
                var conflicts = new List<object>();
                bool gotAnyRows = false;
                bool success = false;
                string? generatedUsernameFromRow = null;

                while (await reader.ReadAsync())
                {
                    gotAnyRows = true;
                    success = reader.GetBoolean(reader.GetOrdinal("Success"));

                    if (!success)
                    {
                        var typeOrdinal = reader.GetOrdinal("ConflictType");
                        var detailsOrdinal = reader.GetOrdinal("Details");
                        var conflictType = reader.IsDBNull(typeOrdinal) ? null : reader.GetString(typeOrdinal);
                        var details = reader.IsDBNull(detailsOrdinal) ? null : reader.GetString(detailsOrdinal);

                        if (!string.IsNullOrEmpty(conflictType))
                            conflicts.Add(new { ConflictType = conflictType, Details = details });
                    }
                    else
                    {
                        var detailsOrdinal = reader.GetOrdinal("Details");
                        generatedUsernameFromRow = reader.IsDBNull(detailsOrdinal) ? null : reader.GetString(detailsOrdinal);
                    }
                }


                if (gotAnyRows && !success)
                {
                    return Conflict(new
                    {
                        error = "Duplicate fields found",
                        conflicts
                        // e.g. [
                        //   { ConflictType: "EMAIL_EXISTS", Details: "Ram@dbs.com" },
                        //   { ConflictType: "PHONE_EXISTS", Details: "903010..." },
                        //   { ConflictType: "NAME_PAIR_EXISTS", Details: "Ram DBS" }
                        // ]
                    });
                }


                if (string.IsNullOrEmpty(generatedUsernameFromRow))
                    generatedUsernameFromRow = usernameParam.Value?.ToString();

                if (string.IsNullOrEmpty(generatedUsernameFromRow))
                    return StatusCode(500, "Username generation failed.");

                // Success: first-time password = username (hash and store)
                var rawPassword = generatedUsernameFromRow;
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(rawPassword);

                using (var updateCmd = new SqlCommand("UPDATE Users SET PasswordHash = @PasswordHash WHERE Username = @Username", conn))
                {
                    updateCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                    updateCmd.Parameters.AddWithValue("@Username", generatedUsernameFromRow);
                    await updateCmd.ExecuteNonQueryAsync();
                }


                // using var userIdCmd = new SqlCommand("SELECT UserId FROM Users WHERE Username = @Username", conn);
                // userIdCmd.Parameters.AddWithValue("@Username", generatedUsernameFromRow);
                // var userIdObj = await userIdCmd.ExecuteScalarAsync();

                return Ok(new
                {
                    Username = generatedUsernameFromRow,
                    Password = rawPassword,
                    Message = "Student registered successfully"
                });
            }
            catch (SqlException ex)
            {
                // If you add DB unique indexes, handle those too
                if (ex.Number == 2627 || ex.Number == 2601)
                {
                    return Conflict(new
                    {
                        error = "Duplicate detected by database index.",
                        sqlError = ex.Message
                    });
                }
                return StatusCode(500, new { error = $"SQL error {ex.Number}: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error: {ex.Message}" });
            }
        }




        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Student_DeleteStudent", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@StudentId", id); // ✅ Fixed here
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Ok(new { message = "Student deleted successfully." });
        }


        [HttpGet("details/{id}")]
        public async Task<ActionResult<object>> GetStudentDetails(int id)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Student_GetStudentDetails", conn) { CommandType = CommandType.StoredProcedure };

            cmd.Parameters.AddWithValue("@UserId", id); // ✅ Correct parameter name

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return Ok(ReadRow(reader));

            return NotFound("Student not found.");
        }


        [HttpGet("GetReferralDetails")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetReferralDetails(string UserCode)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Student_GetReferralDetails", conn) { CommandType = CommandType.StoredProcedure };

            cmd.Parameters.AddWithValue("@UserCode", UserCode);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return Ok(ReadRow(reader));

            return NotFound("Referral Details not found.");
        }



        [HttpGet("profile")]
        public async Task<ActionResult<object>> GetStudentProfile()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { error = "Token missing UserId" });

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_Student_GetStudentProfile", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return Ok(ReadRow(reader));
            return NotFound("Student not found");
        }

        [HttpPut("update/{studentId}")]
        public async Task<IActionResult> UpdateStudent(int studentId, [FromBody] StudentCreateUpdateDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("sp_Student_UpdateStudent", conn) { CommandType = CommandType.StoredProcedure })
                {
                    cmd.Parameters.AddWithValue("@UserId", studentId);
                    cmd.Parameters.AddWithValue("@Email", request.Email);
                    cmd.Parameters.AddWithValue("@FirstName", request.FirstName);
                    cmd.Parameters.AddWithValue("@LastName", request.LastName);
                    cmd.Parameters.AddWithValue("@PhoneNumber", request.PhoneNumber);
                    cmd.Parameters.AddWithValue("@DateOfBirth", request.DateOfBirth);
                    cmd.Parameters.AddWithValue("@Gender", request.Gender);
                    cmd.Parameters.AddWithValue("@Address", request.Address);
                    cmd.Parameters.AddWithValue("@City", request.City);
                    cmd.Parameters.AddWithValue("@State", request.State);
                    cmd.Parameters.AddWithValue("@Country", request.Country);
                    cmd.Parameters.AddWithValue("@ZipCode", request.ZipCode);
                    cmd.Parameters.AddWithValue("@ProfilePhotoUrl", request.ProfilePhotoUrl);
                    cmd.Parameters.AddWithValue("@BatchName", request.Batch);
                    cmd.Parameters.AddWithValue("@ProgrammeId", request.programmeId);
                   // cmd.Parameters.AddWithValue("@GroupId", request.groupId);
                    cmd.Parameters.AddWithValue("@Jsem", request.semester);
                    cmd.Parameters.AddWithValue("@ssem", request.semester);
                    cmd.Parameters.AddWithValue("@degree", request.degree);
                    
                    await cmd.ExecuteNonQueryAsync();
                }

                using var fetchCmd = new SqlCommand("sp_Student_GetStudentDetails", conn) { CommandType = CommandType.StoredProcedure };
                fetchCmd.Parameters.AddWithValue("@UserId", studentId);

                using var reader = await fetchCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        Message = "Student updated successfully",
                        Student = ReadRow(reader)
                    });
                }

                return StatusCode(500, "Update succeeded but student could not be reloaded.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

    }
}
