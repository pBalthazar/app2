using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Facebook;
using SansSoussi.Models;
using SansSoussi.Filter;

namespace SansSoussi.Controllers
{
    [RequireHttps]
    public class AccountController : Controller
    {
        private const int COUNT_BY_MINUTE = 30;

        public IFormsAuthenticationService FormsService { get; set; }
        public IMembershipService MembershipService { get; set; }

        protected override void Initialize(RequestContext requestContext)
        {
            if (FormsService == null) { FormsService = new FormsAuthenticationService(); }
            if (MembershipService == null) { MembershipService = new AccountMembershipService(); }

            base.Initialize(requestContext);
        }

        // **************************************
        // URL: /Account/LogOn
        // **************************************

        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult LogOn()
        {
            return View();
        }

        [HttpPost]
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult LogOn(LogOnModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (MembershipService.ValidateUser(model.UserName, model.Password))
                {
                    FormsService.SignIn(model.UserName, model.RememberMe);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        //Encode the username in base64
                        byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(model.UserName);
                        HttpCookie authCookie = new HttpCookie("username", System.Convert.ToBase64String(toEncodeAsBytes));
                        HttpContext.Response.Cookies.Add(authCookie);
                        return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "The user name or password provided is incorrect.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // **************************************
        // URL: /Account/LogOff
        // **************************************
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult LogOff()
        {
            FormsService.SignOut();

            return RedirectToAction("Index", "Home");
        }

        // **************************************
        // URL: /Account/Register
        // **************************************
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult Register()
        {
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View();
        }

        [HttpPost]
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to register the user
                MembershipCreateStatus createStatus = MembershipService.CreateUser(model.UserName, model.Password, model.Email);

                if (createStatus == MembershipCreateStatus.Success)
                {
                    FormsService.SignIn(model.UserName, false /* createPersistentCookie */);
                    //Encode the username in base64
                    byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(model.UserName);
                    HttpCookie authCookie = new HttpCookie("username", System.Convert.ToBase64String(toEncodeAsBytes));
                    HttpContext.Response.Cookies.Add(authCookie);
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", AccountValidation.ErrorCodeToString(createStatus));
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View(model);
        }

        // **************************************
        // URL: /Account/ChangePassword
        // **************************************

        [Authorize]
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult ChangePassword()
        {
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View();
        }

        [Authorize]
        [HttpPost]
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult ChangePassword(ChangePasswordModel model)
        {
            if (ModelState.IsValid)
            {
                if (MembershipService.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword))
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }
                else
                {
                    ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.PasswordLength = MembershipService.MinPasswordLength;
            return View(model);
        }

        // **************************************
        // URL: /Account/ChangePasswordSuccess
        // **************************************
        [Throttle(TimeUnit = TimeUnit.Minute, Count = COUNT_BY_MINUTE)]
        public ActionResult ChangePasswordSuccess()
        {
            return View();
        }

        private Uri RedirectUri
        {
            get
            {
                var uriBuilder = new UriBuilder(Request.Url);
                uriBuilder.Query = null;
                uriBuilder.Fragment = null;
                uriBuilder.Path = Url.Action("FacebookCallback");
                return uriBuilder.Uri;
            }
        }


        public ActionResult login()
        {
            return View();
        }

        public ActionResult logout()
        {
            FormsAuthentication.SignOut();
            return View("Login");
        }

        public ActionResult Facebook()
        {
            var fb = new FacebookClient();
            var loginUrl = fb.GetLoginUrl(new
            {
                client_id = "266039414050915",
                client_secret = "03e491d0d3f619b42c1183a44b0c72b5",
                redirect_uri = RedirectUri.AbsoluteUri,
                response_type = "code",
                scope = "email" // Add other permissions as needed
            });

            return Redirect(loginUrl.AbsoluteUri);
        }

        public ActionResult FacebookCallback(string code)
        {
            var fb = new FacebookClient();
            dynamic result = fb.Post("oauth/access_token", new
            {
                client_id = "266039414050915",
                client_secret = "03e491d0d3f619b42c1183a44b0c72b5",
                redirect_uri = RedirectUri.AbsoluteUri,
                code = code
            });

            var accessToken = result.access_token;

            // Store the access token in the session for farther use
            Session["AccessToken"] = accessToken;

            // update the facebook client with the access token so 
            // we can make requests on behalf of the user
            fb.AccessToken = accessToken;

            // Get the user's information
            dynamic me = fb.Get("me?fields=first_name,middle_name,last_name,id,email");
            string email = me.email;
            string firstname = me.first_name;
            string middlename = me.middle_name;
            string lastname = me.last_name;
            string facebookId = me.id;

            // Set the auth cookie
            FormsAuthentication.SetAuthCookie(email, false);
            RegisterModel registerModel = new RegisterModel() { Email = email, UserName = email, Password = facebookId, ConfirmPassword = facebookId };
            LogOnModel model = new LogOnModel() { UserName = email, RememberMe = false, Password = facebookId };

            if (ModelState.IsValid)
            {
                if (MembershipService.ValidateUser(model.UserName, model.Password))
                {
                    LogOnFacebook(model);
                }
                else
                {
                    if (RegisterFacebook(registerModel))
                    {
                        LogOnFacebook(model);
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "Cannot logOn with Facebook");
            }

            return RedirectToAction("Index", "Home");
        }
        private void LogOnFacebook(LogOnModel model)
        {
            if (MembershipService.ValidateUser(model.UserName, model.Password))
            {
                FormsService.SignIn(model.UserName, model.RememberMe);
                //Encode the username in base64
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(model.UserName);
                HttpCookie authCookie = new HttpCookie("username", System.Convert.ToBase64String(toEncodeAsBytes));
                HttpContext.Response.Cookies.Add(authCookie);
            }
        }

        private bool RegisterFacebook(RegisterModel model)
        {
            // Attempt to register the user
            MembershipCreateStatus createStatus = MembershipService.CreateUser(model.UserName, model.Password, model.Email);

            if (createStatus == MembershipCreateStatus.Success)
            {
                FormsService.SignIn(model.UserName, false /* createPersistentCookie */);
                //Encode the username in base64
                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(model.UserName);
                HttpCookie authCookie = new HttpCookie("username", System.Convert.ToBase64String(toEncodeAsBytes));
                HttpContext.Response.Cookies.Add(authCookie);
            }
            else
            {
                ModelState.AddModelError("", AccountValidation.ErrorCodeToString(createStatus));
            }
            return createStatus == MembershipCreateStatus.Success;
        }
    }
}
