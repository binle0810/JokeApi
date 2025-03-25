using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions
{
    public interface IEmailService
    {
        Task SendEmailAsync(string userid);
        
        Task SendFavoritesEmailJob();
    }
}
