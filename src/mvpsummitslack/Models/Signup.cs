using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MVPSummitSlack.Models
{
    public class Signup
    {
        [Required(ErrorMessage = "You must enter an e-mail address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid e-mail address.") ]
        public string Email { get; set; }

        [Required(ErrorMessage = "You must enter your first name.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "You must enter your last name.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "You must enter the URL to your public MVP profile.")]
        [Url(ErrorMessage = "Please enter a valid fully-qualified http or https URL.")]
        [Remote("ValidateProfileLink", "Home")]
        public string ProfileLink { get; set; }
    }
}
