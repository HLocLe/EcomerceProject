using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Request.MomoPayment
{
    public class CreateMomoMobilePaymentRequest
    {
        public Guid OrderId { get; set; }
        public string? OrderInfo { get; set; }
    }
}
