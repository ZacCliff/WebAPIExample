using System;
using System.Reflection;
using System.Data;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Security.Cryptography;

namespace WebService
{
  public class UserController : ApiController
  {
    // GET user
    [Route("api/{controller}/GetUser/{Username}/{Password}")]
    public int GetUser(string Username, string Password)
    {
      DataTable zDT = OdbcLib.ExecuteSQLQuerydt("SELECT passwd FROM pub.Users WHERE id = '" + Username + "'", "Users", out string zError);
      if(zDT.Rows.Count > 0)
      {
        string zPasswd = zDT.Rows[0]["passwd"].ToString();
        byte[] zData = System.Text.Encoding.UTF8.GetBytes(zPasswd.ToUpper());
        SHA256Managed zSha = new SHA256Managed();
        byte[] zResult = zSha.ComputeHash(zData);
        string zPasswordKey = BitConverter.ToString(zResult).Replace("-", string.Empty).ToLower();
        if (zPasswordKey == Password)
        {
          Console.WriteLine("User and Password validated");
          return 1;
        }
        else
        {
          Console.WriteLine("Hash of password entered in app does not match hash of password in Database");
          return 0;
        }
      }
      else
      {
        Console.WriteLine("Could not find User or Password in Database");
        return 0;
      }
    }
  }
}
