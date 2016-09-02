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
                    var json = await SlackInvite(model);
                    var ok = (bool)json["ok"];
                    if (ok)
                    {
                        if (!String.IsNullOrWhiteSpace((string)json["warning"]))
                        {
                            Logger.Error($"Attempted to invite {model.Email} and received the following warnings: {(string)json["warning"]}.");
                        }

                        await SendSlackMessage(model);
                        return View("Thanks");
                    }
                    else
                    {
                        Logger.Error($"Attempted to invite {model.Email} and received the following error: {(string)json["error"]}.");
                        ModelState.AddModelError("", TranslateSlackError((string)json["error"]));
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

                default:
                    text = "An error occurred while trying to send the request. Please try again.";
                    break;
            }

            return text;
        }

        private async Task SendSlackMessage(Signup model)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var postData = new JObject(new JProperty("text", $"Invitation sent for\n   *Email:* {model.Email}\n   *Name:* {model.FirstName} {model.LastName}\n   *MVP Profile:* <{model.ProfileLink}>"));
                    var content = new StringContent(postData.ToString(), Encoding.UTF8, "application/json");

                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Calling Slack invite API for {model.Email} with post data {postData.ToString()}.");
                    }

                    var response = await client.PostAsync($"https://hooks.slack.com/services/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", content);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Attempted to post message to the #invites channel for {model.Email} and received an error.");
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
