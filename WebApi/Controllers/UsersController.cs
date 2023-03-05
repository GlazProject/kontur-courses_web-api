using System;
using System.Collections.Generic;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly IUserRepository userRepository;
        private readonly LinkGenerator linkGenerator;
        private readonly IMapper mapper;
        
        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
        public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
            this.linkGenerator = linkGenerator;
        }

        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [HttpHead("{userId}")]
        [Produces("application/json", "application/xml")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var user = userRepository.FindById(userId);
            if (user == null) return NotFound();
            return Ok(mapper.Map<UserDto>(user));
        }

        [HttpPost]
        public IActionResult CreateUser([FromBody] UserForPostDto user)
        {
            if (user == null) return BadRequest();
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            var userEntity = mapper.Map<UserEntity>(user);
            var createdUser = userRepository.Insert(userEntity);
            
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = createdUser.Id },
                createdUser.Id);
        }

        [HttpPut("{userId}")]
        public IActionResult UpdateUser([FromBody] UserForPutDto user, string userId)
        {
            if (user == null) return BadRequest();
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

            if (!Guid.TryParse(userId, out var guid)) return BadRequest();
            var userEntity = mapper.Map(user, new UserEntity(guid));
            userRepository.UpdateOrInsert(userEntity, out var wasInserted);
            
            return wasInserted
                ? CreatedAtRoute(nameof(GetUserById),
                    new { userId = userEntity.Id },
                    userEntity.Id)
                : NoContent();
        }

        [HttpPatch("{userId}")]
        public IActionResult PartiallyUpdateUser([FromBody] JsonPatchDocument<UserForPutDto> patchDoc, string userId)
        {
            if (!Guid.TryParse(userId, out var guid)) return NotFound();
            if (patchDoc == null) return BadRequest();
            var userEntity = userRepository.FindById(guid);
            
            if (userEntity == null) return NotFound();
            var updateDto = mapper.Map<UserForPutDto>(userEntity);
            
            patchDoc.ApplyTo(updateDto, ModelState);
            TryValidateModel(updateDto);
            
            if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
            
            userEntity = mapper.Map(updateDto, new UserEntity(guid));
            userRepository.Update(userEntity);
            return NoContent();
        }

        [HttpDelete("{userId}")]
        public IActionResult DeleteUser(string userId)
        {
            if (!Guid.TryParse(userId, out var guid)) return NotFound();
            if (userRepository.FindById(guid) == null) return NotFound();
            userRepository.Delete(guid);
            return NoContent();
        }

        [HttpGet(Name = nameof(GetUsers))]
        public IActionResult GetUsers([FromQuery] string pageNumber, [FromQuery] string pageSize)
        {
            var pNumber = ConvertPageNumber(pageNumber);
            var pSize = ConvertPageSize(pageSize);

            var pageList = userRepository.GetPage(pNumber, pSize);
            var users = mapper.Map<IEnumerable<UserDto>>(pageList);
            
            var paginationHeader = new
            {
                previousPageLink = pageList.HasPrevious 
                    ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new {pageNumber = pNumber - 1})
                    : null,
                nextPageLink = pageList.HasNext
                    ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new {paheNumber = pNumber + 1})
                    : null,
                totalCount = pageList.TotalCount,
                pageSize = pageList.PageSize,
                currentPage = pageList.CurrentPage,
                totalPages = pageList.TotalPages,
            };
            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
            
            return Ok(users);
        }

        [HttpOptions]
        public IActionResult GetOptions()
        {
            const string allow = "POST, GET, OPTIONS";
            Response.Headers.Add("Allow", allow);
            return Ok();
        }

        private static int ConvertPageNumber(string sPageNumber)
        {
            const int minPageNumber = 1;
            const int defaultPageNumber = 1;
            
            return int.TryParse(sPageNumber, out var pn)
                ? Math.Max(minPageNumber, pn)
                : defaultPageNumber;
        }

        private static int ConvertPageSize(string sPageSize)
        {
            const int minPageSize = 1;
            const int maxPageSize = 20;
            const int defaultPageSize = 10;
            
            return int.TryParse(sPageSize, out var ps)
                ? Constraint(ps, minPageSize, maxPageSize)
                : defaultPageSize;
        }

        private static int Constraint(int value, int min, int max)
        {
            return Math.Min(max, Math.Max(min, value));
        }
    }
}