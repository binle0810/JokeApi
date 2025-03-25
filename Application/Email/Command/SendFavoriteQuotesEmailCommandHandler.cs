using Application.Abstractions;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Email.Command
{
    public class SendFavoriteQuotesEmailHandler : IRequestHandler<SendFavoriteQuotesEmailCommand>
    {
  
        private readonly IEmailService _emailService;

        public SendFavoriteQuotesEmailHandler( IEmailService emailService)
        {

            _emailService = emailService;
        }

        public async Task Handle(SendFavoriteQuotesEmailCommand request, CancellationToken cancellationToken)
        {
      
            await _emailService.SendEmailAsync(request.UserId);
           
        }

      
    }
}
