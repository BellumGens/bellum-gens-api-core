using BellumGens.Api.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace BellumGens.Api.Controllers
{

    [Route("api/[controller]")]
    public class CompaniesController : ControllerBase
    {
        private readonly BellumGensDbContext _dbContext;
        public CompaniesController(BellumGensDbContext context)
        {
            _dbContext = context;
        }
        
        [HttpGet]
        public List<string> Get()
        {
            return _dbContext.Companies.Select(c => c.Name).ToList();
        }
    }
}