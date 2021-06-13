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

        //reading requst body
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        log.LogInformation("Request body converted to string. " + requestBody);

        //get request into an object
        QuoteCreateRequest quote = JsonConvert.DeserializeObject<QuoteCreateRequest>(requestBody);
        log.LogInformation("Request body converted to QuoteCreateRequest." + JsonConvert.SerializeObject(quote));

        var db_connection =
            "Server=tcp:prg-hact-staging-export.database.windows.net,1433;Initial Catalog=pfm-integration;Persist Security Info=False;User ID=adminsql;Password=MCloud360;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30";
        
        using (SqlConnection conn = new SqlConnection(db_connection))
        {
            SqlTransaction transaction = null;
            try
            {
                conn.Open();
                // Start a local transaction.
                transaction = conn.BeginTransaction("EnterQuote");

                int parentEntryNo = -1;
                

                log.LogInformation(
                    "------------------------RUNNIGN QUERY FOR HEADER INSERT-----------------------------");
                parentEntryNo = InsertHeaderAndGetParentEntryNo(log, conn, transaction, quote);

                log.LogInformation(
                    "------------------------RUNNIGN QUERY FOR LINE ITEM INSERT-----------------------------");
                await InsertLineItems(log, quote, conn, transaction, parentEntryNo);

                log.LogInformation(
                    "------------------------RUNNIGN QUERY FOR LINE ITEM INSERT SUCCESS-----------------------------");
                transaction.Commit();
                IsSuccess=true;
                sb.Append("Quote Successfully Added");
                log.LogInformation("------------------------ END -----------------------------");
            }
            catch (Exception ex)
            {
                log.LogInformation("------------------------EXCEPTION-----------------------------" + ex.Message);
                
                sb.Append("Exception occured : "+ ex.GetType() +" "+ex.Message);
                try
                {
                    transaction.Rollback();
                }
                catch (Exception ex2)
                {
                    // This catch block will handle any errors that may have occurred
                    // on the server that would cause the rollback to fail, such as
                    // a closed connection.
                    Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                    Console.WriteLine("  Message: {0}", ex2.Message);
                    sb.Append("Exception occured when trying to rollback: "+ex2.GetType() + " "+ex2.Message);
                }
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("...program execution after database calls...");
            }
        }

        if(IsSuccess){
            Response r = new Response {Status="SUCCESS", Message=sb.ToString()};
            return new OkObjectResult(r );
        }else{
            Response r = new Response {Status="ERROR", Message=sb.ToString()};
            return new BadRequestObjectResult(r );
        }
        // string responseMessage = string.IsNullOrEmpty(name)
        //     ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
        //     : $"Hello, {name}. This HTTP triggered function executed successfully.";

        // return new OkObjectResult(responseMessage);
    }

    private static async Task InsertLineItems(ILogger log, QuoteCreateRequest quote, SqlConnection conn,
        SqlTransaction transaction, int parentEntryNo)
    {
        var lineItemInsertQuery =
            "INSERT INTO [dbo].[HumeQuoteLineOutbound]([parent_entry_no],[sor_code],[description],[location],[qty],[cost])"
            + "VALUES (@parentEntryNo,@sorCode,@description,@location,@qty,@cost)";

        foreach (QuoteLineItem lineItem in quote.QuoteLineItems)
        {
            using (SqlCommand cmd = new SqlCommand(lineItemInsertQuery, conn))
            {
                cmd.Transaction = transaction;

                cmd.Parameters.AddWithValue("@parentEntryNo", parentEntryNo);
                cmd.Parameters.AddWithValue("@sorCode", lineItem.SorCode);
                cmd.Parameters.AddWithValue("@description", lineItem.Description);
                cmd.Parameters.AddWithValue("@location", lineItem.Location);
                cmd.Parameters.AddWithValue("@qty", lineItem.Qty);
                cmd.Parameters.AddWithValue("@cost", lineItem.Cost);
                log.LogInformation("Params set success");


                // Execute the command and log if success.
                var newRawLineItem = await cmd.ExecuteNonQueryAsync();
                if (newRawLineItem > 0)
                {
                    log.LogInformation($"New Line Item Created Successfully ");
                }
            }
        }
    }

    private static int InsertHeaderAndGetParentEntryNo(ILogger log,  SqlConnection conn,
        SqlTransaction transaction, QuoteCreateRequest quote)
    {
        var headerInserQuery =
                    "INSERT INTO [dbo].[HumeQuoteHeaderOutbound] ([work_order_id],[message] ,[quote_date] ,[quote_number], [created_datetime]) "
                    + " VALUES ( @workOrderId,@message,@quoteDate,@quoteNumber,@createdDatetime); SELECT CAST(scope_identity() AS int)";
        int parentEntryNo;
        using (SqlCommand cmd = new SqlCommand(headerInserQuery, conn))
        {
            cmd.Transaction = transaction;

            cmd.Parameters.AddWithValue("@workOrderId", quote.WorkOrderId);
            cmd.Parameters.AddWithValue("@message", quote.Message);
            cmd.Parameters.AddWithValue("@quoteDate", quote.QuoteDate);
            cmd.Parameters.AddWithValue("@quoteNumber", quote.QuoteNumber);
            DateTime timeInAEST = TimeZoneInfo.ConvertTime(DateTime.Now,
                TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time"));
            cmd.Parameters.AddWithValue("@createdDatetime", timeInAEST);
            log.LogInformation("Params set success");


            // Execute the command and log created entry id.
            var newEntryNo = cmd.ExecuteScalar();
            parentEntryNo = (int) newEntryNo;
            log.LogInformation($"New Entry = {newEntryNo} inserted ");
            log.LogInformation(
                "------------------------RUNNIGN QUERY FOR HEADER INSERT SUCCESS-----------------------------");
        }

        return parentEntryNo;
    }
    
    
    public class Response
    {
        [JsonProperty("status")]
     public string Status { get; set; }   
     
        [JsonProperty("message")]
     public string Message { get; set; }   
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

    public class  QuoteCreateRequest
    {
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

    


