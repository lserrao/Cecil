using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeInjectorExperiment
{
    class CodeInjector
    {
        static ModuleDefinition _module;
        static ILProcessor processor;
        static string _outputExe = @"C:\Dropbox\QSPR\Snippets\Learning\MonoCecil\MyLoggerFramework\MyLoggerFramework\bin\Debug\modified\newExe.exe";

        public CodeInjector(string targetAssembly)
        {
            _module = ModuleDefinition.ReadModule(targetAssembly);
        }

        static void Main(string[] args)
        {
            CodeInjector ci = new CodeInjector(@"C:\Dropbox\QSPR\Snippets\Learning\MonoCecil\MyLoggerFramework\MyLoggerFramework\bin\Debug\MyLoggerFramework.exe");
            InjectILCode();
        }

        static void InjectILCode()
        {
            // Get method reference for call flow logging methods
            TypeDefinition callFlowLogger = _module.Types.First(m => m.Name == "TCCallFlow");
            MethodDefinition firstCallFlowLogMethod = callFlowLogger.Methods.First(m => m.Name == "LogCallFlow" && m.Parameters.Count == 8);
            MethodDefinition lastcallFlowLogMethod = callFlowLogger.Methods.First(m => m.Name == "LogCallFlow" && m.Parameters.Count == 5);

            // Target method reference
            TypeDefinition mainClass = _module.Types.First(m => m.Name == "Program");
            //MethodDefinition setRadioTBM = mainClass.Methods.First(m => m.Name == "SetRadio");
            List<MethodDefinition> methods = mainClass.Methods.Where(m => m.IsPublic && !m.IsAbstract).ToList();

            foreach (MethodDefinition method in methods)
            {
                // Get method's IL processor
                processor = method.Body.GetILProcessor();

                #region Inject try-finally into method

                Instruction retInstruction; Instruction firstInstruction;
                fixReturn(method, out firstInstruction, out retInstruction);

                var beforeReturn = Instruction.Create(OpCodes.Nop);
                processor.InsertBefore(retInstruction, beforeReturn);
                processor.InsertBefore(retInstruction, Instruction.Create(OpCodes.Endfinally));

                #endregion

                // Start constructing instructions that need to be injected
                List<Instruction> newInstructions = new List<Instruction>();
                List<Instruction> outParamInitInstructions = new List<Instruction>();

                // Inject first line verbose
                TypeDefinition logger = _module.Types.First(m => m.Name == "Logger");
                MethodDefinition enQVerbose = logger.Methods.First(m => m.Name == "EnqueueVerboseText");
                newInstructions.Add(processor.Create(OpCodes.Ldstr, "First line of method is: " + method.Name));
                newInstructions.Add(processor.Create(OpCodes.Call, enQVerbose));
                //newInstructions.Add(processor.Create(OpCodes.Call, _module.Import(typeof(System.Console).GetMethod("WriteLine", new[] { typeof(string) }))));

                # region Construct call flow logging method at start of method

                // test case name argument
                newInstructions.Add(processor.Create(OpCodes.Ldsfld, _module.Import(typeof(string).GetField("Empty"))));

                // method start time argument
                newInstructions.AddRange(updateTimeStampInstructions(method));

                // GetType argument
                newInstructions.Add(processor.Create(OpCodes.Ldarg_0));
                newInstructions.Add(processor.Create(OpCodes.Call, _module.Import(typeof(System.Type).GetMethod("GetType", new Type[] { }))));

                // method name and description arguments
                newInstructions.Add(processor.Create(OpCodes.Ldstr, method.Name));
                newInstructions.Add(processor.Create(OpCodes.Ldstr, "Description of " + method.Name));

                // string array of parameter names of method
                newInstructions.AddRange(updateInputParamList(method, true));

                // object array of parameter values of method
                newInstructions.AddRange(updateInputParamValues(method, true, out outParamInitInstructions));

                // thread ID argument
                newInstructions.Add(processor.Create(OpCodes.Ldc_I4, 0));

                // call the call flow logging method
                newInstructions.Add(processor.Create(OpCodes.Call, firstCallFlowLogMethod));
                //newInstructions.Add(processor.Create(OpCodes.Call, callFlowLogger.Methods.First(m => m.Name == "experiment")));
                #endregion

                newInstructions.AddRange(outParamInitInstructions);

                foreach (Instruction newInstruction in newInstructions)
                {
                    processor.InsertBefore(firstInstruction, newInstruction);
                }

                #region Construct call flow logging method at end of method

                newInstructions.Clear();

                newInstructions.Add(processor.Create(OpCodes.Nop));
                newInstructions.Add(processor.Create(OpCodes.Ldc_I4, 0));

                // method start time argument
                newInstructions.AddRange(updateTimeStampInstructions(method));

                // method name and description arguments
                newInstructions.Add(processor.Create(OpCodes.Ldstr, method.Name));

                // string array of parameter names of method
                newInstructions.AddRange(updateInputParamList(method, false));

                // object array of parameter values of method
                newInstructions.AddRange(updateInputParamValues(method, false, out outParamInitInstructions));

                newInstructions.Add(processor.Create(OpCodes.Call, lastcallFlowLogMethod));

                #endregion

                // Inject call flow logging method in finally block
                foreach (Instruction newInstruction in newInstructions.Reverse<Instruction>())
                {
                    processor.InsertAfter(beforeReturn, newInstruction);
                }

                var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart = firstInstruction,
                    TryEnd = beforeReturn,
                    HandlerStart = beforeReturn,
                    HandlerEnd = retInstruction
                };

                method.Body.ExceptionHandlers.Add(handler);
                method.Body.InitLocals = true;
            }
            _module.Write(_outputExe);
        }

        static void fixReturn(MethodDefinition method, out Instruction firstInstruction, out Instruction lastInstruction)
        {
            Instruction lastRet;

            if (method.ReturnType == _module.TypeSystem.Void)
            {
                var instructions = method.Body.Instructions;
                lastRet = Instruction.Create(OpCodes.Ret);
                instructions.Add(lastRet);

                var rets = instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();
                foreach (var inst in rets.Take(rets.Count - 1).ToList())
                {
                    inst.OpCode = OpCodes.Leave;
                    inst.Operand = lastRet;
                }
            }
            else
            {
                var instructions = method.Body.Instructions;
                var returnVariable = new VariableDefinition(method.ReturnType);
                method.Body.Variables.Add(returnVariable);
                lastRet = Instruction.Create(OpCodes.Ldloc, returnVariable);
                instructions.Add(lastRet);
                instructions.Add(Instruction.Create(OpCodes.Ret));
                var rets = instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();
                foreach (var inst in rets.Take(rets.Count - 1).ToList())
                {
                    inst.OpCode = OpCodes.Leave;
                    inst.Operand = lastRet;
                    processor.InsertBefore(inst, Instruction.Create(OpCodes.Stloc, returnVariable));
                }
            }

            lastInstruction = lastRet;
            firstInstruction = (method.IsConstructor && !method.IsStatic)
                ? method.Body.Instructions.Skip(2).First()
                : method.Body.Instructions.First();
        }

        static List<Instruction> updateTimeStampInstructions(MethodDefinition method)
        {
            VariableDefinition date_now = new VariableDefinition(_module.Import(typeof(DateTime)));
            method.Body.Variables.Add(date_now);
            method.Body.InitLocals = true;

            List<Instruction> newInstructions = new List<Instruction>();
            newInstructions.Add(processor.Create(OpCodes.Call, _module.Import(typeof(System.DateTime).GetMethod("get_Now")))); // 
            newInstructions.Add(processor.Create(OpCodes.Stloc_S, date_now));
            newInstructions.Add(processor.Create(OpCodes.Ldloca_S, date_now));
            newInstructions.Add(processor.Create(OpCodes.Ldstr, "MM/dd/yyyy hh:mm:ss.fff"));
            newInstructions.Add(processor.Create(OpCodes.Call, _module.Import(typeof(System.DateTime).GetMethod("ToString", new[] { typeof(string) }))));
            return newInstructions;
        }

        static List<Instruction> updateInputParamList(MethodDefinition method, bool excludeOutParams)
        {
            List<Instruction> newInstructions = new List<Instruction>();

            VariableDefinition inputParams = new VariableDefinition(_module.Import(new ArrayType(_module.TypeSystem.String)));
            method.Body.Variables.Add(inputParams);
            method.Body.InitLocals = true;

            int paramsCount = method.Parameters.Count;
            List<ParameterDefinition> parameters = method.Parameters.ToList();
            if (excludeOutParams)
            {
                parameters = method.Parameters.Where(p => !p.IsOut).ToList();
                paramsCount = parameters.Count;
            }

            newInstructions.Add(processor.Create(OpCodes.Ldc_I4, paramsCount));
            newInstructions.Add(processor.Create(OpCodes.Newarr, _module.TypeSystem.String));
            newInstructions.Add(processor.Create(OpCodes.Stloc_S, inputParams));

            int index = 0;
            parameters.ForEach(p =>
            {
                newInstructions.Add(processor.Create(OpCodes.Ldloc_S, inputParams));
                newInstructions.Add(processor.Create(OpCodes.Ldc_I4, index));
                newInstructions.Add(processor.Create(OpCodes.Ldstr, p.ParameterType.Name + " " + p.Name));
                newInstructions.Add(processor.Create(OpCodes.Stelem_Ref));
                index++;
            });
            newInstructions.Add(processor.Create(OpCodes.Ldloc_S, inputParams));
            
            return newInstructions;
        }

        static List<Instruction> updateInputParamValues(MethodDefinition method, bool excludeOutParams, out List<Instruction> outParamsInitInstructions)
        {
            List<Instruction> newInstructions = new List<Instruction>();
            outParamsInitInstructions = new List<Instruction>();

            VariableDefinition inputParamValues = new VariableDefinition(_module.Import(new ArrayType(_module.TypeSystem.Object)));
            method.Body.Variables.Add(inputParamValues);
            method.Body.InitLocals = true;

            int paramsCount = method.Parameters.Count;
            List<ParameterDefinition> parameters = method.Parameters.ToList();
            if (excludeOutParams)
            {
                parameters = method.Parameters.Where(p => !p.IsOut).ToList();
                paramsCount = parameters.Count;
            }

            newInstructions.Add(processor.Create(OpCodes.Ldc_I4, paramsCount));
            newInstructions.Add(processor.Create(OpCodes.Newarr, _module.TypeSystem.Object));
            newInstructions.Add(processor.Create(OpCodes.Stloc_S, inputParamValues));

            int index = 0;
            parameters.ToList().ForEach(p =>
            {
                newInstructions.Add(processor.Create(OpCodes.Ldloc_S, inputParamValues));
                newInstructions.Add(processor.Create(OpCodes.Ldc_I4, index));
                newInstructions.Add(processor.Create(OpCodes.Ldarg, p));

                if (method.Name == "ProcessRefMethod") // still learning, this is very specific to a ref float i'm trying to work out at the moment
                {
                    newInstructions.Add(processor.Create(OpCodes.Ldind_I4));
                    newInstructions.Add(processor.Create(OpCodes.Box, p.ParameterType));
                }
                else
                {
                    if (p.ParameterType.IsValueType)
                        newInstructions.Add(processor.Create(OpCodes.Box, p.ParameterType)); // boxing is needed for value types
                    else
                        newInstructions.Add(processor.Create(OpCodes.Castclass, _module.TypeSystem.Object)); // casting for reference types
                }
                newInstructions.Add(processor.Create(OpCodes.Stelem_Ref));
                index++;
            });
            newInstructions.Add(processor.Create(OpCodes.Ldloc_S, inputParamValues));

            # region Construct instruction to initialize out parameter initialization to inject before try block

            if (method.Parameters.Where(p => p.IsOut).ToList().Count > 0 && method.Name == "RunTraffic")
            {
                outParamsInitInstructions.Add(processor.Create(OpCodes.Nop));
                //for (int i = 0; i < method.Parameters.Count; i++)
                //{
                    //if (method.Parameters[i].IsOut)
                    {
                       // Console.WriteLine("Processing out parameter: " + method.Parameters[i].Name);
                        outParamsInitInstructions.Add(processor.Create(OpCodes.Ldarg_2));
                        outParamsInitInstructions.Add(processor.Create(OpCodes.Ldc_R8, 0.0));
                        outParamsInitInstructions.Add(processor.Create(OpCodes.Stind_R8));
                    }
               // }
            }

            #endregion

            return newInstructions;
        }
    }
}
