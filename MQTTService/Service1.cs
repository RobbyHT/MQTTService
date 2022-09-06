using System;
using MqttNetWrapper;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.IO;

namespace MQTTService
{
    public partial class Service1 : ServiceBase
    {
        private System.Timers.Timer _timer = new System.Timers.Timer();

        public Service1()
        {
            InitializeComponent();
        }

        void Log(string logMessage)
        {
            string logPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/../log";
            string logFileName = DateTime.Now.Year.ToString() + int.Parse(DateTime.Now.Month.ToString()).ToString("00") + int.Parse(DateTime.Now.Day.ToString()).ToString("00") + ".txt";
            string nowTime = int.Parse(DateTime.Now.Hour.ToString()).ToString("00") + ":" + int.Parse(DateTime.Now.Minute.ToString()).ToString("00") + ":" + int.Parse(DateTime.Now.Second.ToString()).ToString("00");

            if (!Directory.Exists(logPath))
            {
                //建立資料夾
                Directory.CreateDirectory(logPath);
            }
            if (!File.Exists(logPath + "/" + logFileName))
            {
                //建立檔案
                File.Create(logPath + "/" + logFileName).Close();
            }
            using (StreamWriter w = File.AppendText(logPath + "/" + logFileName))
            {
                w.Write(nowTime + "---->");
                w.WriteLine(logMessage);
                w.WriteLine("-------------------------------");
            }
        }

        protected override void OnStart(string[] args)
        {
            string cs = "Host=localhost;Username=postgres;Password=1234;Database=xrweb";
            string num = "0";
            string codebookpth = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"/../../configs/XRCodebook.json";
            string connectionpth = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"/../../configs/ConnectionConfig.json";
            WrapperBroker broker = new WrapperBroker(codebookpth, connectionpth);
            List<string> UserGuidList = new List<string>();
            List<string> simList = new List<string>() {"SF", "HA", "SW", "GC"};
            string simType = "";
            int state_0 = 0;
            int state_1 = 0;

            bool check(string username, string password)
            {
                if (username == "admin" && password == "admin")
                    return true;
                return false;
            }
            broker.CheckUserNameAndPassword = check;
            broker.Start();
            Thread.Sleep(3000);


            HttpClient http = new HttpClient();
            http.BaseAddress = new Uri("http://127.0.0.1");
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.Add("stsp", "drRK#04M2P6Eh5r*B1d%");


            //Get MAC Address of this device
            var macList = GetMacList();
            Console.WriteLine($"MacList[0]: {macList[0]}");
            string uid = $"{macList[0]}_{num}";

            //Create a federate client
            Federate client = new Federate();

            //Set Password and UserName if Broker needs them.
            client.SetUserNameAndPassword("admin", "admin");

            //JoinPlatform
            client.JoinPlaform(codebookpth, connectionpth, uid + "GG");

            //Wait for connection
            Console.WriteLine("Connecting:");
            Log("Connecting:");
            while (!client.IsConnected) Thread.Sleep(500);
            Console.WriteLine("Connection Complete!");
            Log("Connection Complete!");

            //Declaring and Subscribing (you must declare object before you register an instance or update its attribute.)
            client.DeclarePublishObjectAttribute("UserData");
            client.DeclarePublishObjectAttribute("Score");
            client.DeclarePublishObjectAttribute("Disaster");
            client.DeclarePublishObjectAttribute("UnitCkpt");
            client.DeclarePublishObjectAttribute("ScoreList");
            client.DeclarePublishObjectAttribute("DisasterList");
            client.DeclarePublishObjectAttribute("CkptSet");
            client.DeclarePublishObjectAttribute("AttendedList");
            Task.Run(async () => {
                await client.SubscribeObjectAttribute("UserData");
                await client.SubscribeObjectAttribute("Score");
                await client.SubscribeObjectAttribute("Disaster");
                await client.SubscribeObjectAttribute("UnitCkpt");
                await client.SubscribeObjectAttribute("ScoreList");
                await client.SubscribeObjectAttribute("DisasterList");
                await client.SubscribeObjectAttribute("CkptSet");
                await client.SubscribeObjectAttribute("AttendedList");
            }).Wait();

            List<string> ScoreListGuidList = new List<string>();
            List<string> DisasterListGuidList = new List<string>();
            List<string> CkptSetGuidList = new List<string>();
            List<string> ScoreGuidList = new List<string>();
            List<string> DisasterGuidList = new List<string>();
            List<string> UnitCkptGuidList = new List<string>();
            List<string> AttendedListGuidList = new List<string>();

            //Assign a function to handle NotifyNewObject
            client.NotifyNewObject +=
                (className, guid) => {
                    Console.WriteLine($"class: {className} has a new instance which guid is {guid}!");
                    switch (className) {
                        case "ScoreList":
                            ScoreListGuidList.Add(guid);
                            break;
                        case "DisasterList":
                            DisasterListGuidList.Add(guid);
                            break;
                        case "CkptSet":
                            CkptSetGuidList.Add(guid);
                            break;
                        case "Score":
                            ScoreGuidList.Add(guid);
                            break;
                        case "Disaster":
                            DisasterGuidList.Add(guid);
                            break;
                        case "UnitCkpt":
                            UnitCkptGuidList.Add(guid);
                            break;
                        case "AttendedList":
                            AttendedListGuidList.Add(guid);
                            break;
                    }
                };

            //Assign a function to handle RecieveObjectAttributes.
            client.RecieveObjectAttributes += PrintUpdateObjectAttributes;

            //Console.Read();
            //----------------------------------------------------------------------------------------------------------------------------------------------------------------

            //Subscribe Interaction
            Task.Run(async () => {
                await client.SubscribeInteraction("UpdateAttendedState");
                await client.SubscribeInteraction("GetAttendedList");
                await client.SubscribeInteraction("GetScoreList");
                await client.SubscribeInteraction("GetDisasterList");
                await client.SubscribeInteraction("GetCkptSet");
            }).Wait();

            //Assign RecieveInteraction
            client.RecieveInteraction = (interactionName, paramDict) =>
            {
                Console.WriteLine($"Recieve interaction:{interactionName}");
                foreach (var p in paramDict)
                    Console.WriteLine($"\tParameter: {p.Key}, Value: {p.Value}");

                switch (interactionName)
                {
                    case "UpdateAttendedState":
                        simType = paramDict["SimType"];
                        if (paramDict["Data"] == "1") {
                            state_1++;
                        }
                        if (paramDict["Data"] == "0")
                        {
                            state_0++;
                        }
                        Task.Run(async () => {
                            foreach (var aGuid in AttendedListGuidList)
                            {
                                await client.DeleteObject("AttendedList", aGuid);
                            }
                            AttendedListGuidList.Clear();

                            await client.RegisterObject("AttendedList");
                            await client.RegisterObject("AttendedList");
                            await client.RegisterObject("AttendedList");
                            await client.RegisterObject("AttendedList");
                        }).Wait();

                        client.DeclarePublishInteraction("GetAttendedList");
                        Task.Run(async () => {
                            var paramsDict = new Dictionary<string, string>()
                                        {
                                            {"Data", paramDict["Data"]}
                                        };
                            await client.SendInteraction("GetAttendedList", paramsDict);
                        }).Wait();
                        break;

                    case "GetAttendedList":
                        if (state_0 !=  state_1) {
                            if (AttendedListGuidList.Count() == 4)
                            {
                                Task.Run(async () =>
                                {
                                    DataTable trainingList = sqlSelect("select * from trainings where training_date = CURRENT_DATE");
                                    
                                    int s = 0;
                                    foreach (var sim in simList)
                                    {
                                        foreach (DataRow training in trainingList.Rows) 
                                        {
                                            var training_id = training["id"].ToString();

                                            DataTable dt = sqlSelect("select trainings.id || '-' || users.job_number || '-' || users.name as id from trainings" +
                                                " inner join training_applies on training_applies.training_id = trainings.id" +
                                                " inner join users on users.id = training_applies.user_id" +
                                                " where trainings.training_date = CURRENT_DATE");

                                            switch (sim)
                                            {
                                                case "SF":
                                                    dt.Rows.Add(training_id + "-11105230001-測試人員01");
                                                    dt.Rows.Add(training_id + "-11105230002-測試人員02");
                                                    dt.Rows.Add(training_id + "-11105230003-測試人員03");
                                                    dt.Rows.Add(training_id + "-11105230004-測試人員04");
                                                    dt.Rows.Add(training_id + "-11105230005-測試人員05");
                                                    break;
                                                case "HA":
                                                    dt.Rows.Add(training_id + "-11105230006-測試人員06");
                                                    dt.Rows.Add(training_id + "-11105230007-測試人員07");
                                                    dt.Rows.Add(training_id + "-11105230008-測試人員08");
                                                    dt.Rows.Add(training_id + "-11105230009-測試人員09");
                                                    dt.Rows.Add(training_id + "-11105230010-測試人員10");
                                                    break;
                                                case "SW":
                                                    dt.Rows.Add(training_id + "-11105230011-測試人員11");
                                                    dt.Rows.Add(training_id + "-11105230012-測試人員12");
                                                    dt.Rows.Add(training_id + "-11105230013-測試人員13");
                                                    dt.Rows.Add(training_id + "-11105230014-測試人員14");
                                                    dt.Rows.Add(training_id + "-11105230015-測試人員15");
                                                    break;
                                                case "GC":
                                                    dt.Rows.Add(training_id + "-11105230016-測試人員16");
                                                    dt.Rows.Add(training_id + "-11105230017-測試人員17");
                                                    dt.Rows.Add(training_id + "-11105230018-測試人員18");
                                                    dt.Rows.Add(training_id + "-11105230019-測試人員19");
                                                    dt.Rows.Add(training_id + "-11105230020-測試人員20");
                                                    break;
                                                default:
                                                    break;
                                            }

                                            string JSONresult = JsonConvert.SerializeObject(dt);

                                            Dictionary<string, string> dic = new Dictionary<string, string>();
                                            dic.Add("SimType", sim);
                                            dic.Add("Data", JSONresult);

                                            Log("Get AttendedList success");
                                            Log(sim);
                                            Log(JSONresult);

                                            await client.UpdateObjectAttribute("AttendedList", AttendedListGuidList[s], dic);
                                        }
                                        s++;
                                    }

                                    Thread.Sleep(1000);
                                    // 持續更新出席人員列表
                                    client.DeclarePublishInteraction("GetAttendedList");
                                    Task.Run(async () =>
                                    {
                                        var paramsDict = new Dictionary<string, string>()
                                            {
                                                {"Data", paramDict["Data"]}
                                            };
                                        await client.SendInteraction("GetAttendedList", paramsDict);
                                    }).Wait();
                                }).Wait();
                            }
                        }else {
                            state_0 = 0;
                            state_1 = 0;
                        }
                        break;

                    case "GetScoreList":
                        Task.Run(async () => {
                            var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(paramDict["Data"]);
                            Console.WriteLine(j);
                            var resp = "";
                            http.DefaultRequestHeaders.Remove("JID");
                            http.DefaultRequestHeaders.Add("JID", j["id"]);
                            HttpResponseMessage response = await http.GetAsync("api/scoreList/" + j["class_code"]);

                            resp = await response.Content.ReadAsStringAsync();
                            Log(resp);

                            if (response.IsSuccessStatusCode)
                            {

                                // Register ScoreList
                                string ScoreListGuid = null;
                                Task.Run(async () => {
                                    var tempTask = client.RegisterObject("ScoreList");
                                    await tempTask;
                                    ScoreListGuid = tempTask.Result;
                                }).Wait();

                                Task.Run(async () => {
                                    Dictionary<string, string> dic = new Dictionary<string, string>();
                                    dic.Add("Data", resp);

                                    await client.UpdateObjectAttribute("ScoreList", ScoreListGuid, dic);
                                }).Wait();
                                Console.WriteLine("Get ScoreList success");
                                Log("Get ScoreList success");
                            }
                        }).Wait();
                        break;

                    case "GetDisasterList":
                        Task.Run(async () => {
                            var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(paramDict["Data"]);
                            var resp = "";
                            http.DefaultRequestHeaders.Remove("JID");
                            http.DefaultRequestHeaders.Add("JID", j["id"]);
                            HttpResponseMessage response = await http.GetAsync("api/disasterList/" + j["class_code"]);

                            resp = await response.Content.ReadAsStringAsync();
                            Log(resp);

                            if (response.IsSuccessStatusCode)
                            {
                                //resp = await response.Content.ReadAsStringAsync();

                                // Register DisasterList
                                string DisasterListGuid = null;
                                Task.Run(async () => {
                                    var tempTask = client.RegisterObject("DisasterList");
                                    await tempTask;
                                    DisasterListGuid = tempTask.Result;
                                }).Wait();

                                Task.Run(async () => {
                                    Dictionary<string, string> dic = new Dictionary<string, string>();
                                    dic.Add("Data", resp);

                                    await client.UpdateObjectAttribute("DisasterList", DisasterListGuid, dic);
                                }).Wait();
                                Console.WriteLine("Get DisasterList success");
                                Log("Get DisasterList success");
                            }
                        }).Wait();
                        break;

                    case "GetCkptSet":
                        Task.Run(async () => {
                            var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(paramDict["Data"]);
                            var resp = "";
                            http.DefaultRequestHeaders.Remove("JID");
                            http.DefaultRequestHeaders.Add("JID", j["id"]);
                            HttpResponseMessage response = await http.GetAsync("api/CkptSet/" + j["class_code"]);

                            resp = await response.Content.ReadAsStringAsync();
                            Log(resp);

                            if (response.IsSuccessStatusCode)
                            {
                                //resp = await response.Content.ReadAsStringAsync();

                                // Register CkptSet
                                string CkptSetGuid = null;
                                Task.Run(async () => {
                                    var tempTask = client.RegisterObject("CkptSet");
                                    await tempTask;
                                    CkptSetGuid = tempTask.Result;
                                }).Wait();

                                Task.Run(async () => {
                                    Dictionary<string, string> dic = new Dictionary<string, string>();
                                    dic.Add("Data", resp);

                                    await client.UpdateObjectAttribute("CkptSet", CkptSetGuid, dic);
                                }).Wait();
                                Console.WriteLine("Get CkptSet success");
                                Log("Get CkptSet success");
                            }
                        }).Wait();
                        break;

                    default:
                        break;
                }
            };
            Console.Read();

            void PrintUpdateObjectAttributes(string classTypeName, string guid, Dictionary<string, string> data)
            {
                Console.WriteLine($"Recieve Class:{classTypeName}'s Attributes:");
                foreach (var d in data)
                    Console.WriteLine($"AttributeName: {d.Key}, AttributeAvlue: {d.Value}");

                //-----------------------------------------------
                switch (classTypeName)
                {
                    case "UserData":
                        if (UserGuidList.IndexOf(guid) == -1)
                        {
                            Task.Run(async () =>
                            {
                                foreach (var sGuid in ScoreListGuidList) {
                                    await client.DeleteObject("ScoreList", sGuid);
                                }
                                foreach (var dGuid in DisasterListGuidList)
                                {
                                    await client.DeleteObject("DisasterList", dGuid);
                                }
                                foreach (var cGuid in CkptSetGuidList)
                                {
                                    await client.DeleteObject("CkptSet", cGuid);
                                }
                                ScoreListGuidList.Clear();
                                DisasterListGuidList.Clear();
                                CkptSetGuidList.Clear();

                                string[] arr = data["ID"].Split('-');

                                DataTable dt = sqlSelect(string.Format("select job_number, account, email, perid, name, gender, born, industries.groups as industry, company, department,   job_title, telphone from users " +
                                    "inner join industries on industries.id = users.industry where users.job_number = '{0}'", arr[1]));


                                if (dt.Rows.Count != 0)
                                {
                                    Dictionary<string, string> dic = new Dictionary<string, string>();
                                    dic.Add("ID", dt.Rows[0]["job_number"].ToString());
                                    dic.Add("Account", dt.Rows[0]["account"].ToString());
                                    dic.Add("Email", dt.Rows[0]["email"].ToString());
                                    dic.Add("PerID", dt.Rows[0]["perid"].ToString());
                                    dic.Add("Name", dt.Rows[0]["name"].ToString());
                                    dic.Add("Gender", dt.Rows[0]["gender"].ToString());
                                    dic.Add("Birth", dt.Rows[0]["born"].ToString());
                                    dic.Add("Industry", dt.Rows[0]["industry"].ToString());
                                    dic.Add("Company", dt.Rows[0]["company"].ToString());
                                    dic.Add("Department", dt.Rows[0]["department"].ToString());
                                    dic.Add("JobTitle", dt.Rows[0]["job_title"].ToString());
                                    dic.Add("Telephone", dt.Rows[0]["telphone"].ToString());

                                    UserGuidList.Add(guid);
                                    Log(dic.ToString());
                                    await client.UpdateObjectAttribute("UserData", guid, dic);
                                    
                                    await client.RegisterObject("Score");
                                    await client.RegisterObject("Disaster");
                                    await client.RegisterObject("UnitCkpt");

                                    // ScoreList
                                    client.DeclarePublishInteraction("GetScoreList");
                                    client.DeclarePublishInteraction("GetDisasterList");
                                    client.DeclarePublishInteraction("GetCkptSet");

                                    Task.Run(async () =>
                                    {
                                        string testJsonStr = "{ \"id\": \"" + arr[1] + "\", \"class_code\": \"" + simType + "\" }";
                                        var paramsDict = new Dictionary<string, string>()
                                        {
                                            { "Data", testJsonStr }
                                        };
                                        await client.SendInteraction("GetScoreList", paramsDict);
                                    }).Wait();

                                    Task.Run(async () =>
                                    {
                                        string testJsonStr = "{ \"id\": \"" + arr[1] + "\", \"class_code\": \"" + simType + "\" }";
                                        var paramsDict = new Dictionary<string, string>()
                                        {
                                            { "Data", testJsonStr }
                                        };
                                        await client.SendInteraction("GetDisasterList", paramsDict);
                                    }).Wait();

                                    Task.Run(async () =>
                                    {
                                        string testJsonStr = "{ \"id\": \"" + arr[1] + "\", \"class_code\": \"" + simType + "\" }";
                                        var paramsDict = new Dictionary<string, string>()
                                        {
                                            { "Data", testJsonStr }
                                        };
                                        await client.SendInteraction("GetCkptSet", paramsDict);
                                    }).Wait();
                                }
                            }).Wait();
                        }
                        break;

                    case "Score":
                        Task.Run(async () => {
                            await client.DeleteObject("Score", guid);
                            ScoreGuidList.Remove(guid);

                            var resp = "";
                            //call http post api with json
                            HttpResponseMessage response = await http.PostAsync("api/score", new StringContent(data["Data"], Encoding.UTF8, "application/json"));
                            Console.WriteLine("score:" + data["Data"]);
                            resp = await response.Content.ReadAsStringAsync();
                            Console.WriteLine("score:" + resp);
                            Log(resp);
                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine("Set score success");
                                Log("Set score success");
                            }
                        }).Wait();
                        break;

                    case "Disaster":
                        Task.Run(async () => {
                            await client.DeleteObject("Disaster", guid);
                            DisasterGuidList.Remove(guid);

                            var resp = "";
                            //call http post api with json
                            HttpResponseMessage response = await http.PostAsync("api/operationDisaster", new StringContent(data["Data"], Encoding.UTF8, "application/json"));

                            resp = await response.Content.ReadAsStringAsync();
                            Console.WriteLine("disaster:" + resp);
                            Log(resp);
                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine("Set disaster success");
                                Log("Set disaster success");
                            }
                        }).Wait();
                        break;

                    case "UnitCkpt":
                        Task.Run(async () => {
                            await client.DeleteObject("UnitCkpt", guid);
                            UnitCkptGuidList.Remove(guid);

                            var resp = "";
                            //call http post api with json
                            HttpResponseMessage response = await http.PostAsync("api/operationCheckpoint", new StringContent(data["Data"], Encoding.UTF8, "application/json"));

                            resp = await response.Content.ReadAsStringAsync();
                            Console.WriteLine("checkpoint:"+ resp);
                            Log(resp);
                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine("Set checkpoint success");
                                Log("Set checkpoint success");
                            }
                        }).Wait();
                        break;

                    default:
                        break;
                }
            }

            List<string> GetMacList()
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                List<string> ML = new List<string>();
                foreach (var nic in nics)
                {
                    // 電腦中可能有很多的網卡(包含虛擬的網卡)，
                    // 只需要 Ethernet 網卡的 MAC
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        ML.Add(nic.GetPhysicalAddress().ToString());
                    }
                }
                return ML;
            }

            DataTable sqlSelect(string str)
            {
                NpgsqlConnection conn = new NpgsqlConnection(cs);
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(str, conn);
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        protected override void OnStop()
        {
        }
    }
}
