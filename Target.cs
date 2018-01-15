using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyLoggerFramework
{
    enum WLANInterface
    {
        STA = 0,
        SAP = 1,
        P2P = 2,
        IBSS = 3
    }

    enum CMDInterface
    {
        GUI,
        CLI
    }

    class Program
    {
        public void SetRadio(bool radioState,  CMDInterface cmdInterface = CMDInterface.GUI)
        {
            Console.WriteLine(string.Format("SetRadio :: radioState = {0}, cmdInterface = {1}", radioState, cmdInterface.ToString()));
            System.Threading.Thread.Sleep(10);
        }

        public void RunTraffic(int pingCount, out double avgMbps, WLANInterface iface = WLANInterface.STA)
        {
            avgMbps = 64.8;
            Console.WriteLine(string.Format("RunTraffic :: pingCount = {0}, iface = {1}", pingCount, iface.ToString()));
        }

        public void ProcessRefMethod(ref int i)
        {
            //str = "changedSample";
            Console.WriteLine("Total average: " + i);
        }

        static void Main(string[] args)
        {
            Program p = new Program();
            //p.SetRadio(true);
            //double avg;
            //p.RunTraffic(10, out avg);
            //string s = "sample";
            int i = 9;
            p.ProcessRefMethod(ref i);
        }
    }
}

namespace Logging
{
    public class Logger
    {
        public static void EnqueueVerboseText(string msg)
        {
            Console.WriteLine(msg);
        }
    }

    public class TCCallFlow
    {
        public static void LogCallFlow(string testCaseName, string startTime, Type type, string methodName, string methodDescription, string[] paramList, object[] paramValues, int threadID = 0)
        {
            Console.WriteLine(string.Format(@"LogCallFlow start of method:: 
                                testCaseName = {0},
                                startTime = {1}, 
                                type = {2},
                                methodName = {3},
                                methodDescription = {4},
                                paramList = {5},
                                paramValue = ""{6}""", testCaseName, startTime, type.Namespace + "." + type.Name, methodName, methodDescription, String.Join(", ", paramList), String.Join(", ", paramValues)));
           
        }

        public static void LogCallFlow(bool forceCommitFlowToFile, string endTime, string methodName, string[] paramList, object[] paramValues)
        {
            Console.WriteLine(string.Format(@"LogCallFlow end of method:: 
                                forceCommitFlowToFile = {0},
                                endTime = {1}, 
                                methodName = {2},
                                paramList = ""{3}"",
                                paramValue = ""{4}""", forceCommitFlowToFile, endTime, methodName, String.Join(", ", paramList), String.Join(", ", paramValues)));
        }

    }
}
