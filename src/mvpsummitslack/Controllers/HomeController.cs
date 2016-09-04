using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MVPSummitSlack.Models;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using System.Text;
using Microsoft.Extensions.Logging;
using NLog;

namespace MVPSummitSlack.Controllers
{
    public class HomeController : Controller
    {
        private readonly SlackSettings _slackSettings;
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        public HomeController(IOptions<SlackSettings> slackSettings)
        {
            _slackSettings = slackSettings.Value;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(Signup model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var profileValidation = await ValidateProfile(model);
                    if (profileValidation.NameVerified)
                    {
                        var json = await SlackInvite(model);
                        var ok = (bool)json["ok"];
                        if (ok)
                        {
                            if (!String.IsNullOrWhiteSpace((string)json["warning"]))
                            {
                                Logger.Error($"Attempted to invite {model.Email} and received the following warnings: {(string)json["warning"]}.");
                            }

                            var postData = $":white_check_mark: Invitation sent for {model.ToSlackMessage()}\n{profileValidation.ToSlackMessage()}";
                            await SendSlackMessage(postData);
                            return View("Thanks");
                        }
                        else
                        {
                            Logger.Error($"Attempted to invite {model.Email} and received the following error: {(string)json["error"]}.");
                            ModelState.AddModelError("", TranslateSlackError((string)json["error"]));
                        }
                    }
                    else
                    {
                        var nameVerificationFailedMessage = $"Profile validation failed for { model.ToSlackMessage()}\n{ profileValidation.ToSlackMessage()}\n\t\t Expected to see `{ profileValidation.NameExpected}` but found `{ profileValidation.NameFound}` instead.";
                        await SendSlackMessage($":x: {nameVerificationFailedMessage}");
                        Logger.Error(nameVerificationFailedMessage);
                        ModelState.AddModelError("ProfileLink", $"The name we found in your MVP profile doesn't match the name you entered.");
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Attempted to invite {model.Email} and received an error.");
                    ModelState.AddModelError("", "An error occurred.");
                }
            }

            return View(model);
        }

        public IActionResult Error()
        {
            return View();
        }

        private string TranslateSlackError(string error)
        {
            var text = "An error occurred while trying to send the request. Please try again.";

            switch (error)
            {
                case "already_in_team":
                    text = "It looks like you're already in the team.";
                    break;

                case "already_invited":
                    text = "It looks like you've already been invited.";
                    break;

                default:
                    text = "An error occurred while trying to send the request. Please try again.";
                    break;
            }

            return text;
        }

        private async Task SendSlackMessage(string message)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var webhookUrl = $"https://hooks.slack.com/services/{_slackSettings.Webhook}";

                    Logger.Debug($"Sending '{message}' to the #invites channel using {webhookUrl}.");

                    var postData = $@"{{ ""text"": ""{message}"" }}";
                    var content = new StringContent(postData, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(webhookUrl, content);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Attempted to send '{message}' to the #invites channel and received an error.");
            }
        }

        private async Task<JObject> SlackInvite(Signup model)
        {
            Logger.Info($@"Calling Slack invite API for request {{""email"": ""{model.Email}"", ""first_name"": ""{model.FirstName}"", ""last_name"": ""{model.LastName}"", ""profile_link"": ""{model.ProfileLink}""}}");

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            using (var client = new HttpClient())
            {
                var token = _slackSettings.Token;
                var postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("set_active", "true"));
                postData.Add(new KeyValuePair<string, string>("token", token));
                postData.Add(new KeyValuePair<string, string>("email", model.Email));
                postData.Add(new KeyValuePair<string, string>("first_name", model.FirstName));
                postData.Add(new KeyValuePair<string, string>("last_name", model.LastName));

                var content = new FormUrlEncodedContent(postData);
                var response = await client.PostAsync($"https://mvpsummit.slack.com/api/users.admin.invite?{secondsSinceEpoch}", content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                return JObject.Parse(result);
            }
        }

        private async Task<ProfileValidation> ValidateProfile(Signup model)
        {
            var validation = new ProfileValidation();

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, model.ProfileLink));
                if (result.IsSuccessStatusCode && !result.RequestMessage.RequestUri.AbsoluteUri.Contains("MvpSearch"))
                {
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.Load(await result.Content.ReadAsStreamAsync());
                    var divElements = doc.DocumentNode.Descendants("div").Where(d => d.Attributes.Contains("class"));

                    // Try to verify that the name matches.
                    var title = divElements.Where(d => d.Attributes["class"].Value.Contains("profile")).SingleOrDefault()?.Descendants("div").FirstOrDefault()?.InnerText.Trim();
                    validation.NameFound = title;
                    validation.NameExpected = model.FullName;
                    validation.NameVerified = title.Contains(model.FullName);
                    bool? foundMatchingEmailAddress = null;

                    // Try to verify that the email address matches.
                    foreach(var emailAddressElement in divElements.Where(d => d.Attributes["class"].Value.Contains("otherPanel")).SingleOrDefault()?.Descendants("a").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("mail")))
                    {
                        if (String.Compare(emailAddressElement.InnerText.Trim(), model.Email) == 0)
                        {
                            foundMatchingEmailAddress = true;
                            break;
                        }

                        foundMatchingEmailAddress = false;
                    }

                    validation.EmailVerified = foundMatchingEmailAddress;
                }
            }

            return validation;
        }

        public async Task<IActionResult> ValidateProfileLink(string profileLink)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, profileLink));
                if (result.IsSuccessStatusCode && !result.RequestMessage.RequestUri.AbsoluteUri.Contains("MvpSearch"))
                {
                    Logger.Debug($"Successfully validated profile link {profileLink}.");
                    return Json("true");
                }

                Logger.Warn($"Failed to validate profile link {profileLink}.");
                return Json("A public MVP profile couldn't be found.");
            }
        }
    }
}
