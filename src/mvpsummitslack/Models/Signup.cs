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
        private string email;
        private string firstName;
        private string lastName;
        private string profileLink;

        [Required(ErrorMessage = "You must enter an e-mail address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid e-mail address.") ]
        public string Email { get { return this.email; } set { this.email = value.Trim(); } }

        [Required(ErrorMessage = "You must enter your first name.")]
        public string FirstName { get { return this.firstName; } set { this.firstName = value.Trim(); } }

        [Required(ErrorMessage = "You must enter your last name.")]
        public string LastName { get { return this.lastName; } set { this.lastName = value.Trim(); } }

        public string FullName { get { return $"{this.FirstName} {this.LastName}"; } }

        [Required(ErrorMessage = "You must enter the URL to your public MVP profile.")]
        [Url(ErrorMessage = "Please enter a valid fully-qualified http or https URL.")]
        [Remote("ValidateProfileLink", "Home")]
        public string ProfileLink { get { return this.profileLink; } set { this.profileLink = value.Trim(); } }

        internal string ToSlackMessage()
        {
            return $"\t*Email:* {this.Email}\n\t*Name:* {this.FullName}\n\t*MVP Profile:* <{this.ProfileLink}>";
        }
    }
}
