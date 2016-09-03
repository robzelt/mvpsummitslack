using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MVPSummitSlack.Models
{
    public class ProfileValidation
    {
        public bool? EmailVerified { get; set; }

        public bool NameVerified { get; set; }

        internal string ToSlackMessage()
        {
            return $"\t*Email Verified:* {this.EmailVerified?.ToString() ?? "unknown"}\n\t*Name Verified:* {this.NameVerified}";
        }
    }
}
