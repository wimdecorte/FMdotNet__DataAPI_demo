using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FMdotNet__DataAPI;
using System.Net;
using System.IO;
using System.Linq;
using System.Configuration;

namespace fmDotNet__DataAPI_Demo
{
    class Program
    {

        static string token;

        static string server = ConfigurationManager.AppSettings.Get("server");
        static string testFile = ConfigurationManager.AppSettings.Get("file");
        static string testLayoutLogin = ConfigurationManager.AppSettings.Get("layout_login");
        static string testLayoutData = ConfigurationManager.AppSettings.Get("layout_data");
        static string testLayoutDataPortal = ConfigurationManager.AppSettings.Get("layout_data_portal");
        static string testAccount = ConfigurationManager.AppSettings.Get("account");
        static string testPassword = ConfigurationManager.AppSettings.Get("password");

        static void Main(string[] args)
        {
            // force the proper TLS version
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            // block the main from exiting
            RunAsync().Wait();
        }


        private static async Task RunAsync()
        {

            DateTime start = DateTime.Now;
            Console.WriteLine(start.ToString());

            int newRecordId = 0;
            int newRecordModId = 0;
            int emptyRecordId = 0;

            // start the connection to FMS
            var fmserver = new FMS(server, testAccount, testPassword);

            // specify what file to work with
            // data from related files will also be available provided
            // that there are table occurrences to it in this target file
            // and that the account has access to it
            fmserver.SetFile(testFile);

            // ---------------------------------------------------------------------------------------------------
            // authenticate and get the token
            fmserver.SetLayout(testLayoutLogin);
            token = await fmserver.Authenticate();
            Console.WriteLine(token);


            // ---------------------------------------------------------------------------------------------------
            // create an empty record
            Console.WriteLine("==> Creating a new empty record...");

            // set the layout you want to interact with
            // all interactions are done through a layout, layout determines what table and what fields
            // basically your context
            fmserver.SetLayout(testLayoutData);
            var response = await fmserver.CreateEmptyRecord();
            if (response.errorCode == 0)
            {
                emptyRecordId = Convert.ToInt32(response.recordId);
                Console.WriteLine("new empty record = " + emptyRecordId.ToString() + " in " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }


            // ---------------------------------------------------------------------------------------------------
            // create a new record and set some data
            Console.WriteLine("==> Creating a new record with data...");
            newRecordId = 0;
            fmserver.SetLayout(testLayoutData);

            // create a new request and specify what data to set in what field
            var request = fmserver.NewRecordRequest();
            request.AddField("country", GetRandomString());

            // execute the request
            response = await request.Execute();
            if (response.errorCode == 0)
            {
                newRecordId = Convert.ToInt32(response.recordId);
                Console.WriteLine("new record = " + newRecordId.ToString() + " in " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }

            // ---------------------------------------------------------------------------------------------------
            // delete a record, we will use the newRecordId that we just created
            Console.WriteLine("==> deleting that last record...");
            fmserver.SetLayout(testLayoutData);
            response = await fmserver.DeleteRecord(newRecordId);
            if (response.errorCode == 0)
            {
                Console.WriteLine(newRecordId.ToString() + " deleted from " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }

            // ---------------------------------------------------------------------------------------------------
            // create a record + a related record
            Console.WriteLine("==> Creating a new record with related data...");
            newRecordId = 0;
            fmserver.SetLayout(testLayoutDataPortal);

            // create the request
            request = fmserver.NewRecordRequest();

            // add data to two fields, these fields belong to the table as set by the layout's context
            // not related fields
            request.AddField("cake", "Yummy Cake");
            request.AddField("country", GetRandomString());


            // using a relationship that does not allow the creation or records will generate
            // error 101 "Record is missing"
            //request.AddRelatedField("fruit", "cake_FRUIT", GetRandomString(), 0);

            // the portal itself does not need to be on the layout
            //fmserver.SetLayout("CAKE_utility__No_Portals");

            // add the related field - use 0 for the related record id to indicate that it is a new record
            // FMS accepts an empty recordId to denote a new related record
            // here were are creating one new record and setting two related fields
            request.AddRelatedField("fruit", "cake_FRUIT__ac", GetRandomString(), 0);
            request.AddRelatedField("number_field", "cake_FRUIT__ac", "7", 0);
            response = await request.Execute();
            if (response.errorCode == 0)
            {
                newRecordId = Convert.ToInt32(response.recordId);
                Console.WriteLine("new record = " + newRecordId.ToString() + " in " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }

            // ---------------------------------------------------------------------------------------------------
            // add one more related record to this parent
            // ==> would be nice if this was a simpler call than having to remember to use 'add related' and 0
            Console.WriteLine("==> Adding a related record to that new record...");

            // create the edit request
            var editRequest = fmserver.EditRequest(newRecordId);

            // add the new related record
            editRequest.AddRelatedField("fruit", "cake_FRUIT__ac", GetRandomString(), 0);
            response = await editRequest.Execute();
            if (response.errorCode == 0)
            {
                Console.WriteLine("related record added to " + newRecordId.ToString() + " in " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }


            // ---------------------------------------------------------------------------------------------------
            // do some edits on the empty record we created earlier
            Console.WriteLine("==> Adding data to the empty record...");
            fmserver.SetLayout(testLayoutData);

            // create the edit request
            editRequest = fmserver.EditRequest(emptyRecordId);

            // modify a field, in this case a repeating field
            editRequest.AddField("field_number_repeat", "100", 1);

            // execute the request
            response = await editRequest.Execute();
            if (response.errorCode == 0)
            {
                Console.WriteLine("data added to record = " + emptyRecordId.ToString() + " in " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }


            // ---------------------------------------------------------------------------------------------------
            // get a record and its portal data
            Console.WriteLine("==> Get all the record data for recordId " + newRecordId.ToString());
            fmserver.SetLayout(testLayoutDataPortal);

            // create a find request, using the record that we create a while ago
            var findRequest = fmserver.FindRequest(newRecordId);

            // execute the record
            var getResponse = await findRequest.Execute();

            if (getResponse.errorCode == 0)
            {
                Console.WriteLine("record count for " + newRecordId.ToString() + " in " + fmserver.CurrentLayout + " is " + getResponse.recordCount + ", with " + getResponse.data.relatedTableNames.Count + " portals");
                newRecordModId = Convert.ToInt32(getResponse.data.foundSet.records[0].modId);
            }
            else
            {
                Console.WriteLine(getResponse.errorCode.ToString());
            }

            // ---------------------------------------------------------------------------------------------------
            // update a specific related record

            // the the Table Occurrence name of the first related set of the first found record, from one of our previous requests
            string TOname = getResponse.data.foundSet.records[0].relatedRecordSets[0].tableName;

            FMRecord firstRelatedRecord = getResponse.data.foundSet.records[0].relatedRecordSets[0].records[0];
            // field name from that related table, and its original value, last field in the list
            KeyValuePair<string, string> kv = firstRelatedRecord.fieldsAndData.Last();
            string oldValue = kv.Value;
            string relatedFieldName = kv.Key;

            // record id for the first related record
            int relatedRecordId = Convert.ToInt32(getResponse.data.foundSet.records[0].relatedRecordSets[0].records[0].recordId);

            // create the edit request
            editRequest = fmserver.EditRequest(newRecordId);

            // modify a field, in this case a repeating field
            string newValue = "99";
            editRequest.AddRelatedField(relatedFieldName, TOname, newValue, relatedRecordId);

            // execute the request
            response = await editRequest.Execute();
            if (response.errorCode == 0)
            {
                Console.WriteLine("data updated on related record = " + relatedRecordId.ToString() + " in " + fmserver.CurrentLayout + ", field = " + relatedFieldName + ", old value = " + oldValue + ", new value = " + newValue);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }


            // ---------------------------------------------------------------------------------------------------
            // get data that same record but with just one portal instead of everything
            Console.WriteLine("==> Get record data plus a selected portal...");

            // get the first portal name from the previous test
            string portalName = getResponse.data.foundSet.records[0].relatedRecordSets[0].tableLayoutObjectName;

            // create the find request
            findRequest = fmserver.FindRequest(newRecordId);

            // specify the portal name to include
            findRequest.AddPortal(portalName);

            // execute the request
            var getResponsePartial = await findRequest.Execute();
            if (getResponsePartial.errorCode == 0)
            {
                Console.WriteLine("record count for " + newRecordId.ToString() + " in " + fmserver.CurrentLayout + " is " + getResponsePartial.recordCount + ", with " + getResponsePartial.data.relatedTableNames.Count + " portals");
            }
            else
            {
                Console.WriteLine(getResponsePartial.errorCode.ToString());
            }


            // ---------------------------------------------------------------------------------------------------
            // try with a portal that does not have a layout object name, does it then use the relationship/TO name?
            // seems not to, always fails.  Not sure if that is a bug - waiting on feedback from FMI.

            Console.WriteLine("==> Get record data plus a selected portal that has no object name...");
            Console.WriteLine("==> EXPECTED TO FAIL WITH ERROR 477, FM requires a named portal.");

            // get the first portal name from the previous test
            portalName = getResponse.data.foundSet.records[0].relatedRecordSets[2].tableName;
            findRequest = fmserver.FindRequest(newRecordId);
            findRequest.AddPortal(portalName);
            getResponsePartial = await findRequest.Execute();
            if (getResponsePartial.errorCode == 0)
            {
                Console.WriteLine("record count for " + newRecordId.ToString() + " in " + fmserver.CurrentLayout + " is " + getResponsePartial.recordCount + ", with " + getResponsePartial.data.relatedTableNames.Count + " portals");
            }
            else
            {
                Console.WriteLine(getResponsePartial.errorCode.ToString());
            }


            // ---------------------------------------------------------------------------------------------------
            // delete related record
            Console.WriteLine("==> delete a related record");

            // ==> can only do one at a time otherwise json complains of duplicate 'relatedRecord" key
            // ==> needs to be fixed to not allow multiple

            // set the context
            fmserver.SetLayout(testLayoutDataPortal);

            // grab the first record
            FMRecord firstRecord = getResponse.data.foundSet.records[0];

            // and the first related record for that record, from the first portal
            firstRelatedRecord = firstRecord.relatedRecordSets[0].records[0];

            // get that record's id
            int firstReleatedRecordId = Convert.ToInt32(firstRelatedRecord.recordId);

            // and the table occurrence for that first related set of records
            string relationship = firstRecord.relatedRecordSets[0].tableName;

            // execute the delete command
            response = await fmserver.DeleteRelatedRecord(newRecordId, relationship, firstReleatedRecordId);
            if (response.errorCode == 0)
            {
                Console.WriteLine("related record " + firstReleatedRecordId.ToString() + " deleted in " + fmserver.CurrentLayout + " from " + relationship + " for parent record " + newRecordId);
            }
            else
            {
                Console.WriteLine(getResponse.errorCode.ToString());
            }



            // ---------------------------------------------------------------------------------------------------
            // create a record and set repeating field
            Console.WriteLine("==> new record and set repeating field...");

            // some housekeeping since we keep reusing this variable
            newRecordId = 0;

            // set the context
            fmserver.SetLayout(testLayoutData);

            // create the request
            request = fmserver.CreateRequest();

            // add a field + value to the request
            request.AddField("country", GetRandomString());

            // add another field, this one is a repeating field
            // set 2nd repetition
            request.AddField("field_number_repeat", "17", 2);

            // execute the request
            response = await request.Execute();
            if (response.errorCode == 0)
            {
                newRecordId = Convert.ToInt32(response.recordId);
                Console.WriteLine("new record created and repeating field set = " + newRecordId + " in " + fmserver.CurrentLayout);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }



            // ---------------------------------------------------------------------------------------------------
            // edit a record and specify a particular mod id
            // hat is the mod id of a newly created record, 0 or 1? --> 0
            // ==> can fail because we didn't correctly capture the newRecordModId from the record we just created
            // we would have to do a Get on it first to know it.
            Console.WriteLine("==> edit a record by specifying both recordId and modId");

            // set the context
            fmserver.SetLayout(testLayoutData);

            // create the request, specifying the target record by id and also specifying the mod id
            var requestEditWithMod = fmserver.EditRequest(newRecordId, newRecordModId);

            // set the value for the target field
            requestEditWithMod.AddField("fruit", "Lorem Ipsum");

            // execute the request
            response = await requestEditWithMod.Execute();
            if (response.errorCode == 0)
            {
                Console.WriteLine("record edited = " + newRecordId + " in " + fmserver.CurrentLayout + " - mod id was " + newRecordModId);
            }
            else
            {
                Console.WriteLine(response.errorCode.ToString() + " - " + response.errorMessage);
            }

            // ---------------------------------------------------------------------------------------------------
            // edit a record with non-matching mod id
            // -> same as above but change the mod id to lower than what is expected


            // ---------------------------------------------------------------------------------------------------
            // do a search
            Console.WriteLine("==> doing a find with criteria for range and start record");

            // set the context
            fmserver.SetLayout(testLayoutDataPortal);

            // create the request
            var getSelectedRecords = fmserver.FindRequest();

            // specify how many records to return
            getSelectedRecords.setHowManyRecords(3);

            // specify the start record (offset)
            getSelectedRecords.setStartRecord(2);

            // execute the request
            var getFindResponse = await getSelectedRecords.Execute();
            if (getFindResponse.errorCode > 0)
            {
                Console.WriteLine(getFindResponse.errorCode.ToString() + " - " + getFindResponse.result);
            }
            else
            {
                // at this point you could serialize these classes in anything you like
                // whatever is supported by the version of .NET for your platform
                // XML, JSON, DataSet, DataTable,...

                // get some output to the console
                PrintOut(getFindResponse);
            }

            // ---------------------------------------------------------------------------------------------------
            // same search and include only two records from the first portal
            Console.WriteLine("==> doing a find with criteria for range and start record, and a sort, and a selected portal with limited records");

            // set context
            fmserver.SetLayout(testLayoutDataPortal);

            // create the request
            getSelectedRecords = fmserver.FindRequest();

            // add a portal, remember that you can use named parameters for any combination of startAt and howMany
            // those are optional parameters
            getSelectedRecords.AddPortal("first_portal", howManyRecords: 1, StartAtRecordNumber: 5);

            // specify the regular found set criteria (range and offset)
            getSelectedRecords.setHowManyRecords(5);
            getSelectedRecords.setStartRecord(10);

            // and also sort that found set
            getSelectedRecords.AddSortField("modification_id", FMS.SortDirection.descend);
            getSelectedRecords.AddSortField("country", FMS.SortDirection.ascend);

            // execute the request
            getFindResponse = await getSelectedRecords.Execute();
            if (getFindResponse.errorCode > 0)
            {
                Console.WriteLine(getFindResponse.errorCode.ToString() + " - " + getFindResponse.result);
            }
            else
            {
                // at this point you could serialize these classes in anything you like
                // whatever is supported by the version of .NET for your platform
                // XML, JSON, DataSet, DataTable,...

                // get some output to the console
                PrintOut(getFindResponse);
            }

            // ---------------------------------------------------------------------------------------------------
            // do an actual search, for values in a field
            Console.WriteLine("==> doing a find with criteria for multiple fields");

            // set the context
            fmserver.SetLayout(testLayoutDataPortal);

            // create the request
            getSelectedRecords = fmserver.FindRequest();

            // add two sets of search criteria (group of fields), these will be executed as an AND search
            var request1 = getSelectedRecords.SearchCriterium();
            request1.AddFieldSearch("country", "belgium");
            request1.AddFieldSearch("cake", "spice");

            var request2 = getSelectedRecords.SearchCriterium();
            request2.AddFieldSearch("country", "belgium");
            request2.AddFieldSearch("cake", "rum");

            // only return related data from the first portal
            getSelectedRecords.AddPortal("first_portal");

            // execute the request
            getFindResponse = await getSelectedRecords.Execute();
            if (getFindResponse.errorCode > 0)
            {
                Console.WriteLine(getFindResponse.errorCode.ToString() + " - " + getFindResponse.result);
            }
            else
            {
                // at this point you could serialize these classes in anything you like
                // whatever is supported by the version of .NET for your platform
                // XML, JSON, DataSet, DataTable,...

                // get some output to the console
                PrintOut(getFindResponse);
            }


            // ---------------------------------------------------------------------------------------------------
            // set a global field
            Console.WriteLine("==> setting a single global field");

            // set the context
            fmserver.SetLayout(testLayoutData);

            // set the global (executes it automatically)
            var globalResponse = await fmserver.SetSingleGlobalField("global_field_text", "Hello World!");

            // get a random record back to confirm the values
            findRequest = fmserver.FindRequest(1);
            getFindResponse = await findRequest.Execute();
            if (getFindResponse.errorCode > 0)
            {
                Console.WriteLine(getFindResponse.errorCode.ToString() + " - " + getFindResponse.result);
            }
            else
            {
                // at this point you could serialize these classes in anything you like
                // whatever is supported by the version of .NET for your platform
                // XML, JSON, DataSet, DataTable,...

                // get some output to the console
                PrintOut(getFindResponse);
            }


            // ---------------------------------------------------------------------------------------------------
            // add multiple globals and get the response back
            Console.WriteLine("==> setting multiple global fields, including repeats");

            // set the context
            fmserver.SetLayout(testLayoutData);

            // create a list of global fields and their values you want to set
            List<Field> fields = new List<Field>();
            fields.Add(new Field("global_field_number", "7"));
            fields.Add(new Field("global_field_text", "Lorem Ipsum"));
            // and setting the 3rd repeat of a global:
            fields.Add(new Field("global_field_number_repeat", 3, "77"));

            // execute it
            globalResponse = await fmserver.SetMultipleGlobalField(fields);

            // get a random record back to confirm the values
            findRequest = fmserver.FindRequest(1);
            getFindResponse = await findRequest.Execute();
            if (getFindResponse.errorCode > 0)
            {
                Console.WriteLine(getFindResponse.errorCode.ToString() + " - " + getFindResponse.result);
            }
            else
            {
                // at this point you could serialize these classes in anything you like
                // whatever is supported by the version of .NET for your platform
                // XML, JSON, DataSet, DataTable,...

                // get some output to the console
                PrintOut(getFindResponse);
            }



            // ---------------------------------------------------------------------------------------------------
            // do something with containers too?
            // stream to base64 text?
            // probably not, FMS give you back a URL to use, you can grab the byte[] and work from that if you need to

            // ---------------------------------------------------------------------------------------------------
            // add a sort by value list name


            // ---------------------------------------------------------------------------------------------------
            // log out
            Console.WriteLine("Logging out...");

            int responseCode = await fmserver.Logout();
            Console.WriteLine("logout response = " + responseCode.ToString());


            // ---------------------------------------------------------------------------------------------------
            // wrapping up
            DateTime end = DateTime.Now;
            double elapsed = (end - start).TotalMilliseconds;
            Console.WriteLine(end.ToString());
            Console.WriteLine("total milliseconds = " + elapsed.ToString());
            Console.WriteLine("");
            Console.Beep();
            Console.WriteLine("Press any key to close this window...");
            Console.ReadKey();




        }

        private static void PrintOut(RecordsGetResponse getFindResponse)
        {
            FMData FMresult = getFindResponse.data;
            FMRecordSet FMrecords = FMresult.foundSet;

            Console.WriteLine(FMrecords.records.Count + " records found in " + testLayoutDataPortal);
            foreach (FMRecord row in FMrecords.records)
            {
                Console.WriteLine("record id = " + row.recordId);
                if (row.relatedRecordSets != null)
                {
                    foreach (FMRecordSet relatedSet in row.relatedRecordSets)
                    {
                        Console.WriteLine("has " + relatedSet.records.Count + " related records in " + relatedSet.tableName);
                    }
                }
                Console.WriteLine("fields for this record:");
                foreach (KeyValuePair<string, string> kv in row.fieldsAndData)
                {
                    Console.WriteLine(kv.Key + " = " + kv.Value);
                }
                Console.WriteLine();
            }
        }

        static string LoremIpsum(int minWords, int maxWords,
            int minSentences, int maxSentences,
            int numParagraphs)
        {

            var words = new[]{"lorem", "ipsum", "dolor", "sit", "amet", "consectetuer",
        "adipiscing", "elit", "sed", "diam", "nonummy", "nibh", "euismod",
        "tincidunt", "ut", "laoreet", "dolore", "magna", "aliquam", "erat"};

            var rand = new Random();
            int numSentences = rand.Next(maxSentences - minSentences)
                + minSentences + 1;
            int numWords = rand.Next(maxWords - minWords) + minWords + 1;

            StringBuilder result = new StringBuilder();

            for (int p = 0; p < numParagraphs; p++)
            {
                result.Append("<p>");
                for (int s = 0; s < numSentences; s++)
                {
                    for (int w = 0; w < numWords; w++)
                    {
                        if (w > 0) { result.Append(" "); }
                        result.Append(words[rand.Next(words.Length)]);
                    }
                    result.Append(". ");
                }
                result.Append("</p>");
            }

            return result.ToString();
        }

        public static string GetRandomString()
        {
            string path = Path.GetRandomFileName();
            path = path.Replace(".", "");
            return path;
        }
    }
}
