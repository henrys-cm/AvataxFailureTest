using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Avalara.AvaTax.RestClient;

namespace AvaTaxFailureTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var companyCode = "CMPL";
 
            var avataxUser = "";
            var avataxPassword = "";
            var avataxClient = new AvaTaxClient("AvalaraFailureTest", "1.0", "", AvaTaxEnvironment.Sandbox)
                .WithSecurity(avataxUser, avataxPassword);

            var pingResult = avataxClient.Ping();
            if (!pingResult.authenticated.Value)
            {
                Console.WriteLine("Auth failed");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Auth success");
            var filepath = "..\\..\\..\\SalesTaxFailures.csv";

            var salesTaxInfo = new List<SalesTaxInput>();

            using (var reader = new StreamReader(filepath))
            {
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    // replace weird strings from export
                    line = Regex.Replace(line, @"[\\""]", "");
                    var values = line.Split(',');

                    try
                    {
                        salesTaxInfo.Add(new SalesTaxInput()
                        {
                            TransactionID = values[0],
                            Amount = decimal.Parse(values[1]),
                            CurrencyCode = values[2],
                            PostalCode = values[3]
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error reading row: {line}");
                        Console.WriteLine(e);
                    }
                }
            }

            foreach (var salesTaxInput in salesTaxInfo)
            {
                // mimicking behaviour in SalesTaxOrchestrator.cs
                // Avatax transaction builder needs hyphen for these big zip codes
                if (salesTaxInput.PostalCode.Length >= 9)
                {
                    salesTaxInput.PostalCode = salesTaxInput.PostalCode.Insert(5, "-");
                }

                var customerCode = $"AvataxFailureTest-{salesTaxInput.TransactionID}";
                try
                {
                    var transactionBuilder = new TransactionBuilder(avataxClient, companyCode, DocumentType.SalesInvoice, customerCode)
                    .WithLine(salesTaxInput.Amount, 1, "SW054000")
                    .WithCurrencyCode(salesTaxInput.CurrencyCode)
                    .WithDescription("Campaign Monitor sales tax transaction")
                    .WithAddress(TransactionAddressType.ShipFrom, "Level 38, 201 Elizabeth Street", null, null, "Sydney", "NSW", "2000", "Australia")
                    .WithAddress(TransactionAddressType.ShipTo, null, null, null, null, null, salesTaxInput.PostalCode, "US");

                    var transactionModel = transactionBuilder.GetCreateTransactionModel();
                    var transaction = avataxClient.CreateTransaction("", transactionModel);

                    Console.WriteLine($"Transaction created on {salesTaxInput.TransactionID} for customer code: {transaction.customerCode}, with status: {transaction.status}, total: {transaction.totalAmount}, taxed: {transaction.totalTax}");
                    //Console.WriteLine("Committing...");
                    //var committedTransaction = avataxClient.CommitTransaction(companyCode, transaction.code, null, "", new CommitTransactionModel { commit = true });

                    //if (committedTransaction.status == DocumentStatus.Committed)
                    //{
                    //    Console.WriteLine("Transaction committed");
                    //}
                    //else
                    //{
                    //    Console.WriteLine("Failed to commit transaction. Please commit manually");
                    //}

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to create transaction for: {salesTaxInput.TransactionID}");
                }
            }

            Console.WriteLine("Finish");
            Console.ReadLine();
        }

        public class SalesTaxInput
        {
            public string TransactionID { get; set; }
            public decimal Amount { get; set; }
            public string CurrencyCode { get; set; }
            public string PostalCode { get; set; }
        }
    }
}
