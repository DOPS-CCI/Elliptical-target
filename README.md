# This archive consists of data and software related to the acquisition and analysis of an experimental protocol in which a subject is asked to locate a randomly generated target within an elliptical target area.
Each trial consists of a hidden generation of a target uniformly within the ellipse; after at least 4 seconds, the subject indicates that she has made a decision; subject then makes her response within the elliptical target area; and the results of the trial are then displayed. An isolated, remote, agent is involved in approximately half of the trials, randomly chosen, in which the target location is displayed to the agent as well as the subsequent results.

In this particular experimental run, there were a total of 13 sessions with a total of 368 trials. BD was the subject and there were two agents, MB and EK, for 10 and 3 of the experimental sessions respectively.
## Trial data - CSV file
Each trial is represented by a line in the file with these items separated by a comma (no header line; distances in cm):
* Agent: Is agent active in this trial?
* AgentID: Identifier of the agent for this trial (may or may not be active for this trial)
* TargetCx: x coordinate of target in reference circle
* TargetCy: y coordinate of target in reference circle
* ResponseCx: x coordinate of response in reference circle
* ResponseCy: y coordinate of response in reference circle
* TargetEx: x coordinate of target in ellipse
* TargetEy: y coordinate of target in ellipse
* ResponseEx: x coordinate of response in ellipse
* ResponseEy: y coordinate of response in ellipse
* RawScore: raw score *s* for this trial
* DateYear: year of the session
* DateMonth: month of the session
* DateDay: day of the session
* SessionID: unique identifier of the session
## Trial data - WFX file for import into Mathematica
Import as a single variable as a List with each entry representing a single trial (see definitions above):

`{Agent,AgentID,{TargetCx,TargetCy},{ResponseCx,ResponseCy},{TargetEx,TargetEy},{ResponseEx,ResponseEy},RawScore,{DateYear,DateMonth,DateDay},SessionID}`
## Experimental protocol program
Application protocol that drives the experimental sessions, trial-by-trial, and records the results
## CCILibrary
Library of routines, some of which are used by the by the experimental protocol program
## CCIUltilities
Another library of routines, some of which are used by the protocol
## RTLibrary
Library of routines which run the experimental protocol with millisecond timing resolution
## Mathematica functions
Mathematica notebooks and package used for performing statistical calculations on the dataset. Also includes a copy of the raw dataset described above.
