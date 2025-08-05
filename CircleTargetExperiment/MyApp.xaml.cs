using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RTLibrary;
using CCIUtilities;

namespace CircleTargetExperiment
{
    public partial class MyApp : RTApplication
    {
        //
        //Default parameter values
        //
        internal static uint BaselineDelay = 1000;
        internal static uint BaselineLength = 120000;
        internal static uint TargetSelectMinDelay = 100;
        internal static uint TargetSelectMaxDelay = 200;
        internal static uint MinPermitDelay = 4000;
        internal static uint FeedbackDelay = 500;
        internal static uint FeedbackLength = 4000;
        internal static uint totalTrialsInRun = 50;
        internal static double agentProb = 0.5D;
        private readonly string RWNLFile = "./CirclePsi.xml";

        RTExperiment experiment;

        //
        //Screens
        //
        RTTechScreen techScreen;
        RTSubjectScreen subjScreen;
        RTScreen agentScreen;

        //
        //Baseline trial Events
        //
        RTTrial baselineTrial;
        RTEvent beginBaseline;
        RTEvent endBaseline;

        //
        //Protocol trial Events
        //
        RTTrial protocolTrial;
        RTAwaitEvent beginProtocolTrialEvent;
        RTEvent earlyDecision;
        RTEvent delaySelection;
        RTAwaitEvent targetSelection;
        RTAwaitEvent permitTimeout; //response before this event causes abort of trial
        RTAwaitEvent awaitDecision;
        RTAwaitEvent awaitResponse;
        RTEvent beginFeedback;

        //
        //Tech, Subject, and Agent display panels
        //
        TechPanel techPanel;
        SubjectBeginNewTrial subjectBeginTrial;
        SubjectPanel subjectTargetPanel;
        SubjectAbortPanel abortMessagePanel;
        AgentPanel agentPanel;
        AgentMessagePanel agentMessagePanel;

        int trialNumberInRun = 0;
        int runNumber = 0;
        int agentGV = 1; // 1 => Yes on this trial, 2 => No agent on this trial, 3 => No agent participation.
        bool agentPresent;
        readonly PCG32 pcg32 = PCG32.Instance; //RNG
        readonly SoundPlayer sp = new SoundPlayer("./sfx-magic.wav"); //permit period sound

        public MyApp()
        {
            AgentSetup askAgent = new AgentSetup();
            if (!(bool)askAgent.ShowDialog()) Environment.Exit(-1);
            agentPresent = (bool)askAgent.AgentPresent.IsChecked;
            if (!agentPresent) agentGV = 3;
            askAgent = null;

            //Create the Experiment based on xml outline
            experiment = new RTExperiment(RWNLFile, agentPresent, cleanup: informAgent, epilogue: finalResults);

            //
            //Set up tech and subject screens
            //

            techPanel = new TechPanel(this);
            techScreen = RTDisplays.TechScreen;
            techScreen.AddKeyCodes("QECASB", new RTTechScreen.Command[6] { Quit, End, Continue, Abort, StartNewRun, Baseline });
            techScreen.AddPanel(techPanel);

            subjectBeginTrial = new SubjectBeginNewTrial();
            subjectTargetPanel = new SubjectPanel();
            abortMessagePanel = new SubjectAbortPanel();
            abortMessagePanel.MinWait.Text =
                Math.Round((double)(TargetSelectMaxDelay + MinPermitDelay) / 1000D).ToString("0");

            subjScreen = RTDisplays.SubjectScreen;
            subjScreen.AddPanel(subjectBeginTrial);
            subjScreen.AddPanel(subjectTargetPanel);
            subjScreen.AddPanel(abortMessagePanel);

            if (agentGV != 3) //then Agent display will be used
            {
                agentScreen = experiment.Displays.AgentScreen;
                agentPanel = new AgentPanel(agentScreen);
                agentScreen.AddPanel(agentPanel);
                agentMessagePanel = new AgentMessagePanel();
                agentScreen.AddPanel(agentMessagePanel);
            }

            //
            //Instantiate Trial objects
            //  Create beginning and end Events and Events within trial 
            //

            //Baseline trial
            beginBaseline =
                experiment.InstantiateRTEvent("BeginBaseline", BaselineDelay);
            endBaseline =
                experiment.InstantiateRTEvent("EndBaseline", BaselineLength);
            baselineTrial = new RTTrial(experiment, beginBaseline, baselineCleanup, postAbortBaselineCleanup);

            //Main protocol trial

            beginProtocolTrialEvent = new RTAwaitEvent(experiment.InstantiateRTEvent("BeginTrial"));
            earlyDecision = experiment.InstantiateRTEvent("EarlyDecision");
            delaySelection = experiment.InstantiateRTEvent("TargetSelection");
            targetSelection = new RTAwaitEvent(earlyDecision, timeoutEvent: delaySelection);
            permitTimeout = new RTAwaitEvent(earlyDecision,
                timeoutEvent: experiment.InstantiateRTEvent("BeginPermitPeriod", MinPermitDelay));
            awaitDecision = new RTAwaitEvent(experiment.InstantiateRTEvent("Decision"));
            awaitResponse = new RTAwaitEvent(experiment.InstantiateRTEvent("Response"));
            beginFeedback =
                experiment.InstantiateRTEvent("BeginFeedback", FeedbackDelay);
            protocolTrial = new RTTrial(experiment, beginProtocolTrialEvent, cleanupProtocolTrial, postAbortProtocolTrial);

            //Start the session, displaying B and tech panels
            experiment.Displays.BeginDisplays();
            techScreen.ShowPanel(techPanel);
            techScreen.TechMessage("Warn subject of baseline acquisition and enter B");
            if (agentPresent)
                agentScreen.ShowPanel(agentMessagePanel); //indicate session beginning to agent, if present
        }

        #region MainProtocol

        #region BeginTrial
        [AssociatedEvent("BeginTrial", AssociatedEventType.Immediate)]
        private RTEvent beginProtocolTrialIM() => targetSelection;

        [AssociatedEvent("BeginTrial", AssociatedEventType.Delayed_UI)]
        private void beginProtocolTrialUI(RTEventGV gv)
        {
            subjScreen.HideAll();
            techScreen.TechMessage($"Begin trial {trialNumberInRun:0} in run {runNumber:0}");
            if (agentPresent) agentScreen.HideAll();
#if RTTrace
            experiment.Trace.Write("BeginTrial UI completed");
#endif
        }
        #endregion

        #region TargetSelection
        [AssociatedEvent("TargetSelection", AssociatedEventType.Immediate)]
        private RTEvent TargetSelectionIM()
        {
            selectTargetValue();
            return permitTimeout;
        }

        ulong tTime;
        [AssociatedEvent("TargetSelection", AssociatedEventType.Delayed_UI)]
        private void TargetSelectionUI(RTEventGV gv)
        {
            tTime = gv.ClockTime;
            targetX = targetR * Math.Cos(targetTheta);
            targetY = targetR * Math.Sin(targetTheta);
            if (agentGV == 1)
            {
                agentPanel.MoveTarget(targetX, targetY);
                agentScreen.ShowPanel(agentPanel);
            }
            gv["TargetX"] = (int)(targetX * 1000D);
            gv["TargetY"] = (int)(targetY * 1000D);
            gv["TargetR"] = (int)(targetR * 1000D);
#if RTTrace
            experiment.Trace.Write("TargetSelection UI completed");
#endif
        }

        double targetX;
        double targetY;
        double targetR;
        double targetTheta;
        private void selectTargetValue()
        {
            targetR = Math.Sqrt(pcg32.Generate());
            targetTheta = 2D * Math.PI * pcg32.Generate();
        }
        #endregion

        #region EarlyDecision

        [AssociatedEvent("EarlyDecision", AssociatedEventType.Immediate)]
        private RTEvent EarlyDecisionIM()
        {
            enableA = false;
            return new RTAbortTrialEvent(1); //insert trial abort Event
        }

        [AssociatedEvent("EarlyDecision", AssociatedEventType.Delayed_UI)]
        private void EarlyDecisionUI(RTEventGV _)
        {
#if RTTrace
            experiment.Trace.Write("Early decision has occured; Abort trial pending");
#endif
            techPanel.DisableKey(techPanel.Abort);
            subjScreen.ShowPanel(abortMessagePanel);
            if (agentGV == 1)
            {
                agentScreen.HidePanel(agentPanel);
                agentMessagePanel.AgentMessage.Text = "Aborted trial";
                agentScreen.ShowPanel(agentMessagePanel);
            }
#if RTTrace
            experiment.Trace.Write("EarlyDecision UI completed");
#endif
        }
        #endregion

        #region BeginPermitPeriod
        [AssociatedEvent("BeginPermitPeriod", AssociatedEventType.Immediate)]
        private RTEvent permitIM() => awaitDecision;

        [AssociatedEvent("BeginPermitPeriod", AssociatedEventType.Delayed_UI)]
        private void permitUI(RTEventGV gv)
        {
            techScreen.TechMessage("Permit response");
#if RTTrace
            experiment.Trace.Write("BeginPermitPeriod UI completed");
#endif
        }
        #endregion

        #region Decision
        [AssociatedEvent("Decision", AssociatedEventType.Immediate)]
        private RTEvent DecisionIM() => awaitResponse;

        [AssociatedEvent("Decision", AssociatedEventType.Delayed_UI)]
        private void DecisionUI(RTEventGV gv)
        {
            subjectTargetPanel.initializeCursor();
            subjScreen.ShowPanel(subjectTargetPanel);
            techScreen.TechMessage("Decision made");
            gv["DecisionT"] = (int)(gv.ClockTime - tTime);
#if RTTrace
            experiment.Trace.Write("Decision UI completed");
#endif
        }
        #endregion

        #region Response
        [AssociatedEvent("Response", AssociatedEventType.Immediate)]
        private RTEvent ResponseIM() => beginFeedback;

        double responseX;
        double responseY;
        double responseR;

        [AssociatedEvent("Response", AssociatedEventType.Delayed_UI)]
        private void ResponseUI(RTEventGV gv)
        {
            enableA = false;
            techPanel.DisableKey(techPanel.Abort);
            responseX = subjectTargetPanel.cursor.X / subjectTargetPanel.circleR;
            responseY = -subjectTargetPanel.cursor.Y / subjectTargetPanel.circleR;
            responseR = subjectTargetPanel.radius;
            gv["ResponseX"] = (int)(responseX * 1000D);
            gv["ResponseY"] = (int)(responseY * 1000D);
            gv["ResponseR"] = (int)(responseR * 1000D);
#if RTTrace
            experiment.Trace.Write("Response UI completed");
#endif
        }
        #endregion

        #region BeginFeedback
        [AssociatedEvent("BeginFeedback", AssociatedEventType.Immediate)]
        private RTEvent BeginFeedbackIM() => protocolTrial.End((uint)FeedbackLength);

        double rho;
        double score;
        double scoresum = 0;
        [AssociatedEvent("BeginFeedback", AssociatedEventType.Delayed_UI)]
        private void BeginFeedbackUI(RTEventGV ev)
        {
            if (agentGV == 1)
                agentPanel.ShowResponse(responseX, responseY);
            techPanel.PB.Value = (double)trialNumberInRun / totalTrialsInRun;
            techScreen.TechMessage($"End trial {trialNumberInRun:0} of run {runNumber:0}; total trials = {protocolTrial.CurrentTrialNumber:0}");

            rho = Math.Sqrt(Math.Pow(responseX - targetX, 2) + Math.Pow(responseY - targetY, 2));
            score = conditionalCDF(rho, responseR);
            scoresum += score;

            subjectTargetPanel.showTargetResponse(trialNumberInRun, new Point(targetX, targetY), new Point(responseX, responseY));
#if RTTrace
            experiment.Trace.Write("BeginFeedback UI completed");
#endif
        }

        public static double conditionalCDF(double rho, double r)
        {
            if (rho <= 0D) return 0D;
            double rho2 = rho * rho;
            double rm1 = 1D - r;
            if (rho <= rm1) return rho2;
            double rp1 = 1D + r;
            if (rho < rp1)
            {
                double a1 = Math.Acos((rho2 + r * r - 1D) / (2D * r * rho));
                double a2 = Math.Acos((rho2 - r * r - 1D) / (2D * r));
                double t =
                    Math.Sqrt((rho2 - rm1 * rm1) * (rp1 * rp1 - rho2)) - 2D * rho2 * a1 + 2D * a2;
                return 1D - t / (2D * Math.PI);
            }
            return 1D;
        }
        #endregion

        #endregion

        private void cleanupProtocolTrial(RTTrial trial)
        {
            //reset Subject screen
            subjectTargetPanel.hideTargetResponse();
            if (agentGV == 1)
                agentScreen.HidePanel(agentPanel);

            //Backfill missing GVs in all Events
            int tX = trial.GetGV("TargetSelection", "TargetX");
            int tY = trial.GetGV("TargetSelection", "TargetY");
            int tR = trial.GetGV("TargetSelection", "TargetR");

            for (int i = 0; i < trial.EventCount; i++)
            {
                trial.SetGV(i, "TargetX", tX);
                trial.SetGV(i, "TargetY", tY);
                trial.SetGV(i, "TargetR", tR);
            }

            int rX = trial.GetGV("Response", "ResponseX");
            int rY = trial.GetGV("Response", "ResponseY");
            int rR = trial.GetGV("Response", "ResponseR");

            RTEventGV gv = trial.GetEventGVByName("BeginTrial");
            gv["ResponseX"] = rX;
            gv["ResponseY"] = rY;
            gv["ResponseR"] = rR;
            gv = trial.GetEventGVByName("TargetSelection");
            gv["ResponseX"] = rX;
            gv["ResponseY"] = rY;
            gv["ResponseR"] = rR;
            gv = trial.GetEventGVByName("Decision");
            gv["ResponseX"] = rX;
            gv["ResponseY"] = rY;
            gv["ResponseR"] = rR;
            gv = trial.GetEventGVByName("BeginFeedback");
            gv["ResponseX"] = rX;
            gv["ResponseY"] = rY;
            gv["ResponseR"] = rR;

            int irho = (int)(rho * 1000D);
            int iscore = (int)(score * 1000D);
            trial.SetAllGVByName("TrialNumber", protocolTrial.CompletedTrialNumber);
            trial.SetAllGVByName("RunTrialNumber", trialNumberInRun);
            trial.SetAllGVByName("RunNumber", runNumber);
            trial.SetAllGVByName("Agent", agentGV);
            trial.SetAllGVByName("Rho", irho);
            trial.SetAllGVByName("Score", iscore);
#if RTTrace
            experiment.Trace.Write("Trial cleanup completed");
#endif
            if (trialNumberInRun < totalTrialsInRun) //loop back for next trial
            {
                //schedule beginning of next trial
                beginNextTrial();
                return;
            }

#if RTTrace
            experiment.Trace.Write($"Completed run number {runNumber:0}");
#endif
            if (agentPresent)
            {
                agentMessagePanel.AgentMessage.Text = $"End of run {runNumber:0}";
                agentScreen.ShowPanel(agentMessagePanel);
            }
            enableA = false;
            enableS = enableE = true;
            techPanel.DisableKey(techPanel.Abort);
            techPanel.EnableKey(techPanel.Start);
            techPanel.EnableKey(techPanel.End);
            techScreen.TechMessage($"Completed run {runNumber:0}. Total trials = {protocolTrial.CompletedTrialNumber}. Inform subject and ask to continue.");
        }

        private void postAbortProtocolTrial(int reason) //post-abort routine for the main protocol trial
        {
            //reason 1 is subject makes early decision
            //reason 2 is tech aborts trial manually
            if (reason == 2)
            {
                subjScreen.HidePanel(subjectBeginTrial);
                subjectTargetPanel.hideTargetResponse(); //in case they're showing
            }
            if (agentGV == 1 && agentPanel.Visibility == Visibility.Visible) //agentGV == 1 and not pre-TargetSelection
            {
                agentScreen.HidePanel(agentPanel);
                agentMessagePanel.AgentMessage.Text = "Aborted trial";
                agentScreen.ShowPanel(agentMessagePanel);
            }
            enableC = enableE = true; //enable C and E keys
            techPanel.EnableKey(techPanel.Continue);
            techPanel.EnableKey(techPanel.End);
            trialNumberInRun--;

            techScreen.TechMessage($"Aborted trial {protocolTrial.CompletedTrialNumber + 1:0} because of " +
                    (reason == 1 ? "subject early decision" : "tech key") + "; enter C to continue; E to end session.");
#if RTTrace
            experiment.Trace.Write("AbortTrial UI completed");
#endif
        }

        #region Baseline trial

        [AssociatedEvent("BeginBaseline", AssociatedEventType.Immediate)]
        private RTEvent BeginBaselineIM() => endBaseline;

        [AssociatedEvent("BeginBaseline", AssociatedEventType.Delayed_UI)]
        private void BeginBaselineUI(RTEventGV _)
        {
            enableA = true;
            techPanel.EnableKey(techPanel.Abort);
            techScreen.TechMessage("Begin baseline acquisition");
#if RTTrace
            experiment.Trace.Write("BeginBaseline UI completed");
#endif
        }

        [AssociatedEvent("EndBaseline", AssociatedEventType.Immediate)]
        private RTEvent EndBaselineIM() => baselineTrial.End();

        private void baselineCleanup(RTTrial _) //be sure Baseline has finished before disabling
        {
            enableB = false; //remains enabled until fully completed, in case of abort
            techPanel.DisableKey(techPanel.Baseline);
            enableA = false; //disable Abort
            techPanel.DisableKey(techPanel.Abort);
            enableS = true;
            techPanel.EnableKey(techPanel.Start);
            techScreen.TechMessage("End baseline acquisition; inform subject and prepare to start main protocol with S");
#if RTTrace
            experiment.Trace.Write("Baseline trial cleanup completed");
#endif
        }

        private void postAbortBaselineCleanup(int reason)
        {
            enableB = true;
            techPanel.EnableKey(techPanel.Baseline);
            techScreen.TechMessage("Aborted baseline acquisition; enter B to restart baseline");
#if RTTrace
            experiment.Trace.Write("Baseline trial abort cleanup completed");
#endif
        }

        #endregion

        #region Tech key commands

        bool enableS = false;
        DateTime? startTime = null;
        private void StartNewRun(ModifierKeys _) //Tech key S
        {
            if (enableS)
            {
                enableS = enableE =  false;
                techPanel.DisableKey(techPanel.Start);
                techPanel.DisableKey(techPanel.End);

                runNumber++;
                trialNumberInRun = 0;
                if (agentPresent)
                {
                    agentMessagePanel.AgentMessage.Text= $"Start run {runNumber:0}";
                    agentScreen.ShowPanel(agentMessagePanel);
                }
                techScreen.TechMessage($"Start protocol run {runNumber:0}");
                techPanel.PB.Value = 0D;
                if(!startTime.HasValue) startTime = DateTime.Now; //record start time only for first run in session

                beginNextTrial();
            }
        }

        bool enableA = false;
        private void Abort(ModifierKeys _) //Tech key A
        {
            if (enableA)
            {
                RTTrial.AbortAny(2); //this will abort either baseline or protocol trials
                enableA = false;
                techPanel.DisableKey(techPanel.Abort);
            }
        }

        bool enableB = true;
        private void Baseline(ModifierKeys _) //Tech key B
        {
            if (enableB)
            {
                enableB = false; //one time only
                techPanel.DisableKey(techPanel.Baseline);
                baselineTrial.Begin();
            }
        }

        bool enableC = false;
        private void Continue(ModifierKeys _) //Tech key C
        {
            if (enableC)
            {
                beginNextTrial();
                enableC = enableE = false;
                techPanel.DisableKey(techPanel.Continue);
                techPanel.DisableKey(techPanel.End);
                techScreen.TechMessage($"Continue trial");
                subjScreen.HidePanel(abortMessagePanel);
            }
        }

        bool enableE = false;
        private void End(ModifierKeys _) //Tech key E
        {
            if (enableE)
            {
                enableS = enableC = enableE = enableQ = false;
                techPanel.DisableKey(techPanel.Start);
                techPanel.DisableKey(techPanel.Continue);
                techPanel.DisableKey(techPanel.End);
                techPanel.DisableKey(techPanel.Quit);
                techScreen.TechMessage("Ending experimental session");
                subjScreen.HideAll(); //In case abort message showing
                NormalEndOfExperiment();
            }
        }

        bool enableQ = true;
        private void Quit(ModifierKeys mod) //Tech key cntrlQ
        {

            if (!enableQ || mod != ModifierKeys.Control) return;
#if RTTrace || RTTraceUAId
            experiment.Trace.Write("Quit by tech");
            experiment.Trace.Display();
#endif
            Environment.Exit(-1); //Abnormal shutdown does not write RWNL dataset
        }
        #endregion

        private void beginNextTrial()
        {
            delaySelection.Delay = (uint)pcg32.Generate(TargetSelectMinDelay, TargetSelectMaxDelay);
            trialNumberInRun++;
            subjectBeginTrial.TrialNumber.Text = trialNumberInRun.ToString("0");
            protocolTrial.Begin();
            enableA = true;
            techPanel.EnableKey(techPanel.Abort);
            if (agentPresent)
            {
                agentPanel.Reset(); //clear last response
                agentGV = pcg32.Generate() < agentProb ? 1 : 2; //next trial agent status
            }
            subjectTargetPanel.initializeCursor(); //make sure cursor on screen
            subjScreen.ShowPanel(subjectBeginTrial);
        }

        private void NormalEndOfExperiment()
        {
            calculatePrelimScore();
            string prelimResults = $"***Preliminary results*** -- N = {completedTrials:0}; score = {sc:0.00}; p-val = {pval:0.000}";
            experiment.End(sessionComment: prelimResults);
        }

        private void informAgent()
        {
            if (agentPresent)
            {
                agentScreen.HideAll();
                agentMessagePanel.AgentMessage.Text = "End of session";
                agentScreen.ShowPanel(agentMessagePanel);
            }
        }

        int completedTrials;
        double pval;
        double sc;
        private void calculatePrelimScore()
        {
            completedTrials = protocolTrial.CompletedTrialNumber;
            pval = IrwinHall.CDF(scoresum, completedTrials);
            sc = -10D * Math.Log10(scoresum / completedTrials);
        }

        private void finalResults()
        {
            string d = (DateTime.Now - (DateTime)startTime).ToString(@"h\:mm\:ss");
            FinalStats stats = new FinalStats();
            stats.NTrials.Text = completedTrials.ToString("0");
            stats.PVal.Text = pval.ToString("0.000");
            stats.ScoreTot.Text = sc.ToString("0.00");
            stats.Time.Text = d;
            stats.ShowActivated = true;
            stats.Topmost = true;
            stats.ShowDialog();
            Log.writeToLog($"** Circle Target Experiment({experiment.SoftwareVersion}) ** N = {completedTrials:0}; Time = {d}; Score = {sc:0.00}; P-val = {pval:0.000}");
        }
    }
}
