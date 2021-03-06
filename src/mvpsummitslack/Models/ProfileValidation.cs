﻿using System;
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

        public string NameFound { get; set; }

        public string NameExpected { get; set; }

        internal string ToSlackMessage()
        {
            return $"\t\t*Name Verified:* {this.NameVerified}, *Email Verified:* {this.EmailVerified?.ToString() ?? "N/A"}";
        }
    }
}
