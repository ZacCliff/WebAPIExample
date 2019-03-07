using System;
using System.Reflection;
using System.Data;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WebService
{
  public class BookingController : ApiController
  {
    string m_Message = "";
    string m_Error = "";
    int m_ResultInt;
    DataTable m_DataTable;

    // GET api/booking/GetBookings/killdate/lot/chain/species
    [Route("api/{controller}/GetAllBookings/{BookingDate}/{Days}")]
    //[GzipCompression]
    public DataTable GetAllBookings(string BookingDate, int Days)
    {
      DateTime zCurrentDate = Convert.ToDateTime(BookingDate);
      string zFromDateString = zCurrentDate.ToString("yyyy-MM-dd");
      string zToDateString = zCurrentDate.AddDays(-Days).ToString("yyyy-MM-dd");
      Console.WriteLine("Getting All Bookings from " + zFromDateString + " to " + zToDateString);
      m_DataTable = OdbcLib.ExecuteSQLQuerydt("SELECT BookingDate, BookingNo, BookingType FROM pub.Booking WHERE BookingDate <= '" + zFromDateString + "' AND BookingDate >= '" + zToDateString + "' AND NOT BookingType = 'YC' ORDER BY BOOKINGDATE DESC", "Booking", out m_Error);
      return m_DataTable;
    }

    // PUT api/booking/PutBookingToDatabase/BookingData
    [Route("api/{controller}/PutBookingToDatabase/{BookingData}")]
    public string PutBookingToDatabase([FromBody] Booking ABooking)
    {
      string zBookingDeserializedDate = ABooking.BOOKINGDATE.ToString("yyyy-MM-dd");
      Console.WriteLine("Saving Booking to Database");
      m_ResultInt = OdbcLib.ExecuteScalar("SELECT COUNT(*) FROM pub.Booking WHERE BookingNo ='" + ABooking.BOOKINGNO + "' AND BookingDate ='" + zBookingDeserializedDate + "'", out m_Error);

      if (m_ResultInt == 1)
      {
        m_ResultInt = OdbcLib.ExecuteSQLNonQuery("UPDATE pub.Booking SET BookingNo = '" + ABooking.BOOKINGNO + "', BookingDate = '" + zBookingDeserializedDate + "', " +
            "BookingType = '" + ABooking.BOOKINGTYPE + "' WHERE BookingNo ='" + ABooking.BOOKINGNO + "' AND BookingDate ='" + zBookingDeserializedDate + "'", out m_Error);

        if (m_ResultInt == 1)
        {
          Console.WriteLine("Update Booking to Database Successful on " + DateTime.Now + "");
          m_Message = "Update Booking to Database Successful";
        }
        else
        {
          Console.WriteLine("Update Booking to Database Unsuccessful on " + DateTime.Now + "");
          m_Message = "Update Booking to Database Unsuccessful. Check the logs for more info.";
        }
      }
      else
      {
        m_ResultInt = OdbcLib.ExecuteSQLNonQuery("INSERT INTO pub.Booking (BookingNo, BookingDate, BookingType) VALUES ('" + ABooking.BOOKINGNO +
        "','" + zBookingDeserializedDate + "','" + ABooking.BOOKINGTYPE + "')", out m_Error);

        if (m_ResultInt == 1)
        {
          Console.WriteLine("Save Booking to Database Successful on " + DateTime.Now + "");
          m_Message = "Save Booking to Database Successful";
        }
        else
        {
          Console.WriteLine("Save Booking to Database Unsuccessful on " + DateTime.Now + "");
          m_Message = "Save Booking to Database Unsuccessful. Check the logs for more info.";
        }
      }

      return m_Message;
    }
  }

  public class EncodingDelegateHandler : DelegatingHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      return base.SendAsync(request, cancellationToken).ContinueWith<HttpResponseMessage>((responseToCompleteTask) =>
      {
        HttpResponseMessage response = responseToCompleteTask.Result;

        if (response.RequestMessage.Headers.AcceptEncoding != null &&
            response.RequestMessage.Headers.AcceptEncoding.Count > 0)
        {
          string encodingType = response.RequestMessage.Headers.AcceptEncoding.First().Value;

          response.Content = new CompressedContent(response.Content, encodingType);
        }

        return response;
      },
      TaskContinuationOptions.OnlyOnRanToCompletion);
    }
  }

  public class CompressedContent : HttpContent
  {
    private HttpContent originalContent;
    private string encodingType;

    public CompressedContent(HttpContent content, string encodingType)
    {
      if (encodingType == null)
      {
        throw new ArgumentNullException("encodingType");
      }

      originalContent = content ?? throw new ArgumentNullException("content");
      this.encodingType = encodingType.ToLowerInvariant();

      if (this.encodingType != "gzip" && this.encodingType != "deflate")
      {
        throw new InvalidOperationException(string.Format("Encoding '{0}' is not supported. Only supports gzip or deflate encoding.", this.encodingType));
      }

      // copy the headers from the original content
      foreach (KeyValuePair<string, IEnumerable<string>> header in originalContent.Headers)
      {
        this.Headers.TryAddWithoutValidation(header.Key, header.Value);
      }

      this.Headers.ContentEncoding.Add(encodingType);
    }

    protected override bool TryComputeLength(out long length)
    {
      length = -1;

      return false;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
      Stream compressedStream = null;

      if (encodingType == "gzip")
      {
        compressedStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
      }
      else if (encodingType == "deflate")
      {
        compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
      }

      return originalContent.CopyToAsync(compressedStream).ContinueWith(tsk =>
      {
        if (compressedStream != null)
        {
          compressedStream.Dispose();
        }
      });
    }
  }
}
