#r "Newtonsoft.Json"

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");
    StringBuilder sb = new StringBuilder("");
    bool IsSuccess = false;
    List<Quote> quotes = new List<Quote>(); 

    Quote temp = new Quote();

    var db_connection =
            "Server=tcp:prg-hact-staging-export.database.windows.net,1433;Initial Catalog=pfm-integration;Persist Security Info=False;User ID=adminsql;Password=MCloud360;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30";
        
    try{
        using (SqlConnection conn = new SqlConnection(db_connection))
        {
            try
            {
                conn.Open();
                log.LogInformation("------------------------RUNNIGN QUERY FOR EXTRACT -----------------------------");
                //Query for loading header values
                var selectHeadersQuery =
                    "SELECT [entry_no],[work_order_id],[message],[quote_date],[quote_number] FROM [dbo].[HumeQuoteHeaderOutbound]"
                    + "WHERE (([processed_datetime] is null ) OR ( [Error] = 1)) ";
        
                using (SqlCommand cmd = new SqlCommand(selectHeadersQuery, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            temp = new Quote();

                            temp.EntryNo = (int)reader["entry_no"];
                            temp.WorkOrderId = (int)reader["work_order_id"];
                            temp.Message = (string)reader["message"];
                            temp.QuoteDate = ((DateTime)reader["quote_date"]).ToString("MM/dd/yyyy HH:mm");
                            temp.QuoteNumber = int.Parse(reader["quote_number"].ToString());

                            //Add Quote objct to list
                            quotes.Add(temp);
                            log.LogInformation("------------------------RUNNIGN QUERY FOR HEADER LOAD SUCCESS-----------------------------");
                            
                        }
                    }
                }

                //Load line items and attachments from dependant tables
                foreach( Quote q in quotes)
                {
                    List<QuoteLineItem> items = GetLineItemsForQuote(log, conn,  q.EntryNo);
                    List<string> attachments = GetAttachmentsForQuote(log, conn,  q.EntryNo);
                    q.QuoteLineItems = items;
                    q.Attachments = attachments;
                }

                log.LogInformation("------------------------RUNNIGN QUERY FOR EXTRACT SUCCESS-----------------------------");
                
                IsSuccess=true;
                log.LogInformation("------------------------ END -----------------------------");
            }
            catch (Exception ex)
            {
                log.LogInformation("------------------------EXCEPTION-----------------------------" + ex.Message);
                sb.Append("Exception occured : "+ ex.GetType() +" "+ex.Message);
            }
        }    

    }catch (Exception e)
    {
        log.LogInformation("------------------------EXCEPTION-----------------------------" + e.Message);
        sb.Append("Exception occured : "+ e.GetType() +" "+e.Message);
    }

    if(!IsSuccess)
    {
        //Return bad request if exception occured
        return new BadRequestObjectResult(sb.ToString());
    }
    //Return List of Quotes serializing to Json
    return new OkObjectResult(JsonConvert.SerializeObject(quotes));
}

private static List<QuoteLineItem> GetLineItemsForQuote(ILogger log,  SqlConnection conn, int parentEntryNo)
    {
        List<QuoteLineItem> lineItems = new List<QuoteLineItem>();
        QuoteLineItem tempLineItem = new QuoteLineItem();

        var selectLineItemsQuery =
                    "SELECT [sor_code],[description],[location],[qty],[cost] FROM [dbo].[HumeQuoteLineOutbound]"
                    + "WHERE [parent_entry_no] = @parentEntryNo";

        using (SqlCommand cmd = new SqlCommand(selectLineItemsQuery, conn))
        {
            cmd.Parameters.AddWithValue("@parentEntryNo", parentEntryNo);

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tempLineItem = new QuoteLineItem();

                    tempLineItem.SorCode = (string)reader["sor_code"];
                    tempLineItem.Description = (string)reader["description"];
                    tempLineItem.Location = (string)reader["location"];
                    tempLineItem.Qty = int.Parse(reader["qty"].ToString());
                    tempLineItem.Cost = double.Parse(reader["cost"].ToString());

                    lineItems.Add(tempLineItem);

                    log.LogInformation("------------------------RUNNIGN QUERY FOR LINE ITEM LOAD SUCCESS-----------------------------");
                            
                }
            }
        }
        return lineItems;
    }

private static List<string> GetAttachmentsForQuote(ILogger log,  SqlConnection conn, int parentEntryNo)
    {
        List<string> links = new List<string>();

        var selectLineItemsQuery = "SELECT [link] FROM [dbo].[HumeAttachments]"
            + "WHERE [parent_entry_no] = @parentEntryNo AND [direction]=1";

        using (SqlCommand cmd = new SqlCommand(selectLineItemsQuery, conn))
        {
            cmd.Parameters.AddWithValue("@parentEntryNo", parentEntryNo);
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string link = (string)reader["link"];
                    links.Add(link);
                }
            }
        }
        
        log.LogInformation("------------------------RUNNIGN QUERY FOR ATTACHEMNTS ITEM LOAD SUCCESS-----------------------------");
        return links;
    }

public class QuoteLineItem
    {
        [JsonProperty("sor_code")]
        public string SorCode { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("qty")]
        public int Qty { get; set; }

        [JsonProperty("cost")]
        public double Cost { get; set; }
    }

    

    public class  Quote
    {
        [JsonIgnore]
        public int EntryNo { get; set; }

        [JsonProperty("work_order_id")]
        public int WorkOrderId { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("quote_number")]
        public int QuoteNumber { get; set; }

        [JsonProperty("quote_date")]
        public string QuoteDate { get; set; }

        [JsonProperty("attachments")]
        public List<string> Attachments { get; set; }

        [JsonProperty("quote_line_items")]
        public List<QuoteLineItem> QuoteLineItems { get; set; }

    }

