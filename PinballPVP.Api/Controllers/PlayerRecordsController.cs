using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/users/[controller]")]
public class PlayerRecordsController : ControllerBase
{
    private readonly PinballPVPContext _context;

    public PlayerRecordsController(PinballPVPContext context)
    {
        _context = context;
    }

    
}