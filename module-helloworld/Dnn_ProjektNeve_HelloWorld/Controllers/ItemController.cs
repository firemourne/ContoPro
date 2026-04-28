/*
' Copyright (c) 2026 Contopro
'  All rights reserved.
' 
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
' DEALINGS IN THE SOFTWARE.
' 
*/

using DotNetNuke.Entities.Users;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using ProjektNeve.Dnn.Dnn_ProjektNeve_HelloWorld.Components;
using ProjektNeve.Dnn.Dnn_ProjektNeve_HelloWorld.Models;
using System;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Web.Mvc;
using Hotcakes.Commerce;
using Hotcakes.Commerce.Catalog;
using Hotcakes.Commerce.Orders;
using Hotcakes.Commerce.Dnn;

namespace ProjektNeve.Dnn.Dnn_ProjektNeve_HelloWorld.Controllers
{
    [DnnHandleError]
    public class ItemController : DnnController
    {

        public ActionResult Delete(int itemId)
        {
            ItemManager.Instance.DeleteItem(itemId, ModuleContext.ModuleId);
            return RedirectToDefaultRoute();
        }

        public ActionResult Edit(int itemId = -1)
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.DnnPlugins);

            var userlist = UserController.GetUsers(PortalSettings.PortalId);
            var users = from user in userlist.Cast<UserInfo>().ToList()
                        select new SelectListItem { Text = user.DisplayName, Value = user.UserID.ToString() };

            ViewBag.Users = users;

            var item = (itemId == -1)
                 ? new Item { ModuleId = ModuleContext.ModuleId }
                 : ItemManager.Instance.GetItem(itemId, ModuleContext.ModuleId);

            return View(item);
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Edit(Item item)
        {
            if (item.ItemId == -1)
            {
                item.CreatedByUserId = User.UserID;
                item.CreatedOnDate = DateTime.UtcNow;
                item.LastModifiedByUserId = User.UserID;
                item.LastModifiedOnDate = DateTime.UtcNow;

                ItemManager.Instance.CreateItem(item);
            }
            else
            {
                var existingItem = ItemManager.Instance.GetItem(item.ItemId, item.ModuleId);
                existingItem.LastModifiedByUserId = User.UserID;
                existingItem.LastModifiedOnDate = DateTime.UtcNow;
                existingItem.ItemName = item.ItemName;
                existingItem.ItemDescription = item.ItemDescription;
                existingItem.AssignedUserId = item.AssignedUserId;

                ItemManager.Instance.UpdateItem(existingItem);
            }

            return RedirectToDefaultRoute();
        }

        [ModuleAction(ControlKey = "Edit", TitleKey = "AddItem")]
        public ActionResult Index()
        {
            var items = ItemManager.Instance.GetItems(ModuleContext.ModuleId);
            return View(items);
        }



        //Konyveles.cshtml megjelenítése
        [HttpGet]
        [ActionName("Konyveles")]
        public ActionResult Konyveles()
        {
            return View();
        }

        //Summary.cshtml megjelenítese
        [HttpGet]
        [ActionName("Summary")]
        public ActionResult Summary()
        {
            // Nyers adatok kiolvasása az URL-ből
            string nyersCeg = Request.QueryString["ceg"];
            string nyersAdo = Request.QueryString["ado"];
            string nyersBizonylat = Request.QueryString["bizonylat"];
            string nyersLetszam = Request.QueryString["letszam"] ?? "0";
            string nyersAfa = Request.QueryString["afa"];

            // --- 2. SZÁMOLÁSI LOGIKA ---
            double alapAr = 25000;

            // Cégforma szorzó
            double formaSzorzo = (nyersCeg == "kft") ? 1.6 : 1.0;

            // Bizonylat felár
            double bizonylatPlusz = 0;
            if (nyersBizonylat == "21-50") bizonylatPlusz = 15000;
            else if (nyersBizonylat == "51-100") bizonylatPlusz = 30000;
            else if (nyersBizonylat == "100+") bizonylatPlusz = 60000;

            // Alkalmazott felár (szövegből számmá alakítjuk)
            int alkalmazottSzam = 0;
            int.TryParse(nyersLetszam, out alkalmazottSzam);
            double alkalmazottDij = Math.Abs(alkalmazottSzam) * 4000;

            // ÁFA szorzó (pl. ha ÁFA körös, rászámolunk 27%-ot, azaz 1.27-tel szorozzuk)
            double afaSzorzo = (nyersAfa == "afas") ? 1.27 : 1.0;

            // VÉGSŐ ÁR KISZÁMÍTÁSA
            double kalkulaltOsszeg = ((alapAr * formaSzorzo) + bizonylatPlusz + alkalmazottDij) * afaSzorzo;
            ViewBag.VegsoAr = Math.Round(kalkulaltOsszeg);






            // 1. Cégforma szépítése
            ViewBag.Cegforma = (nyersCeg == "kft") ? "Kft. / Bt." : "Egyéni vállalkozó";

            // 2. Adózási mód szépítése (Switch szerkezettel)
            string szepAdo = "";
            switch (nyersAdo)
            {
                case "ata": szepAdo = "Átalányadó"; break;
                case "kata": szepAdo = "KATA"; break;
                case "tao": szepAdo = "TAO (Társasági adó)"; break;
                case "kiva": szepAdo = "KIVA"; break;
                default: szepAdo = "Ismeretlen"; break;
            }
            ViewBag.Adozas = szepAdo;

            // 3. Bizonylatszám szépítése
            if (nyersBizonylat == "100+")
            {
                ViewBag.Bizonylat = "100 db felett";
            }
            else
            {
                ViewBag.Bizonylat = nyersBizonylat + " db";
            }

            // 4. Létszám (a "fő" szót a HTML-ben már odaírtuk, ide csak a szám kell)
            ViewBag.Letszam = nyersLetszam;

            // 5. ÁFA kör szépítése
            ViewBag.Afa = (nyersAfa == "afas") ? "ÁFA körös (27%)" : "Alanyi Adómentes (AAM)";

            return View();
        }



        //kosárba gomb backend megvalósítása


        [HttpPost]
        [AllowAnonymous]
        public ActionResult AddToCart(double vegsoAr)
        {
            //// 1. Hotcakes alkalmazás példányosítása a jelenlegi portálon
            //var motor = HotcakesApplication.Current;

            //// 2. A termék megkeresése (Használd a Hotcakes-ben megadott SKU-t!)
            //var product = motor.CatalogServices.Products.FindBySku("TK1");

            //if (product != null)
            //{
            //    // 3. LineItem létrehozása a termékből
            //    var lineItem = product.ConvertToLineItem(motor, 1);

            //    // 4. AZ ÁR FELÜLÍRÁSA - Itt adjuk át a kalkulált értéket
            //    lineItem.BasePricePerItem = (decimal)vegsoAr;
            //    lineItem.CustomProperties.Add("hcc", "priceoverridden", "1");

            //    // VAGY próbáld meg így (ha a using Hotcakes.Commerce.Orders kint van):
            //    // lineItem.IsPriceOverridden = true;

            //    // 5. Kosárba rakás
            //    var basket = motor.OrderServices.CurrentShoppingCart
            //    motor.OrderServices.AddItemToOrder(basket, lineItem);

            //    // 6. Átirányítás a kosárhoz
            //    // Ha a RouteHccRelative nem megy, használd ezt a "fapados", de biztos utat:
            //    return Redirect("/cart");
            //}

            var app = HotcakesApplication.Current;

            var product = app.CatalogServices.Products.FindBySku("TK1");
            if (product == null)
                return RedirectToAction("Summary");

            var lineItem = product.ConvertToLineItem(app, 1);

            // ideiglenes, egyedi ár
            lineItem.BasePricePerItem = (decimal)vegsoAr;
            lineItem.AdjustedPricePerItem = (decimal)vegsoAr;
            lineItem.LineTotal = (decimal)vegsoAr * lineItem.Quantity;

            // fontos: így kell custom property-t írni
            lineItem.CustomPropertySet("custom", "priceoverridden", "1");

            // fontos: user supplied price esetén a Hotcakes külön árú tételként kezeli
            lineItem.IsUserSuppliedPrice = true;

            var basket = app.OrderServices.EnsureShoppingCart();

            app.OrderServices.AddItemToOrder(basket, lineItem);

            // EZ HIÁNYZOTT: mentés
            app.OrderServices.Orders.Upsert(basket);
            app.OrderServices.InvalidateCachedCart();

            return Redirect("/HotcakesStore/Cart");
        }


    }
}
