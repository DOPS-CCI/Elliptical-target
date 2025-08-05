(* ::Package:: *)

BeginPackage["EllipticalTarget`"];


fEllipse::usage="fEllipse[\[Rho],\[Alpha],r] is the value of the conditional PDF of the RV \[Rho],
the distance between target t and the response r,
for the given value of r in an ellipse with x-semiaxis \[Alpha] and y-semiaxis 1,
assuming a uniformly distibuted RV t within the ellipse";


FEllipse::usage="FEllipse[\[Rho],\[Alpha],r] is the value of the conditional CDF of the RV \[Rho],
the distance between target t and the response r,
for the given value of r in an ellipse with x-semiaxis \[Alpha] and y-semiaxis 1,
assuming a uniformly distibuted RV t within the ellipse";


f::usage="f[\[Rho],r] is the value of the conditional PDF of the RV \[Rho], 
the distance between target t and the response r,
for the given value of r in a circle of radius 1,
assuming a uniformly distibuted RV t within the circle"


F::usage="F[\[Rho],r] is the value of the conditional CDF of the RV \[Rho], 
the distance between target t and response r,
for the given value of r in a circle of radius 1,
assuming a uniformly distibuted RV t within the circle"


Begin["`Private`"];


(* ::Input::Initialization:: *)
CubicReal[A2_,A1_,A0_]:=Module[{q=A1/3-A2^2/9,r=(3 A0-A1 A2)/6+A2^3/27,A,t1,\[Theta]},(*Depressed cubic x^3+3qx+2r*)
A=r^2+q^3;(*Equivalent to Sqrt[r^2/4+q^3/27]*)
If[A>0,(*Only 1 real root*)
A=(Abs[r]+Sqrt[A])^(1/3);
t1={(A-q/A)If[r<=0,1,-1]},
(* else 3 real roots*)
Assert[q<=0,"CubicReal: q>0"];
\[Theta]=If[q==0,0,Re[ArcCos[r/(q Sqrt[-q])]]];
t1=2Sqrt[-q] { Cos[\[Theta]/3],Cos[\[Theta]/3-(2\[Pi])/3],Cos[\[Theta]/3+(2\[Pi])/3]}
];
t1-A2/3
]


(* ::Input::Initialization:: *)
Find\[Lambda]d[\[Rho]_,\[Alpha]_,{xr_,yr_}]:=(*Calculate \[Lambda]s resulting in degenerative conics*)
Sort[CubicReal[(1-xr^2-yr^2+\[Alpha]^2+\[Rho]^2)/\[Alpha]^2,(-xr^2+\[Rho]^2+\[Alpha]^2 (1-yr^2+\[Rho]^2))/\[Alpha]^4,\[Rho]^2/\[Alpha]^4],\[CapitalDelta]2CE[#1,\[Alpha]]<\[CapitalDelta]2CE[#2,\[Alpha]]&]


(* ::Input::Initialization:: *)
LEintersection[{a_,b_,c_},\[Alpha]_/;NumberQ[\[Alpha]]]:=Module[{d=b^2-c^2+a^2 \[Alpha]^2},If[Im[c]!=0,Nothing,If[d>0,d=Sqrt[d];1/(b^2+a^2 \[Alpha]^2) {{-\[Alpha] (b d+c \[Alpha] a),-b c+\[Alpha] d a},{\[Alpha] (b d-c \[Alpha] a),-b c-\[Alpha] d a}},Nothing]]]


(* ::Input::Initialization:: *)
intersections[pair_/;Length[pair]==2,\[Alpha]_/;NumberQ[\[Alpha]]]:=LEintersection[#,\[Alpha]]&/@pair


(* ::Input::Initialization:: *)
hyperbola[\[Alpha]_,{xr_,yr_},\[Lambda]_]:=(*Asymptotes of degenerate rectilinear hyperbola (crossing pair of lines)*)
With[{m=Sqrt[-((1+\[Lambda])/(1+\[Alpha]^2 \[Lambda]))]},{{m,1,(xr-m yr)/(m(1+\[Alpha]^2 \[Lambda]))},{-m,1,-((xr+m yr)/(m(1+\[Alpha]^2 \[Lambda])))}}]


(* ::Input::Initialization:: *)
parallel[\[Rho]_,\[Alpha]_,{xr_,yr_}]:=(*Pair of parallel lines from degenerate parabola; either vertical or horizontal; \[Lambda]\[Equal]-1 if horizontal and -1/\[Alpha]^2 if vertical *)
Module[{t=\[Alpha]^2-1,horizontal=Abs[xr]<Abs[yr],B,A},
If[horizontal,
B=-yr;
A=Sqrt[\[Alpha]^4+\[Rho]^2+\[Alpha]^2 (-1+yr^2-\[Rho]^2)];
{{0,1,(-B+A)/t},{0,1,(-B-A)/t}},
B=\[Alpha]^2 xr;
A=\[Alpha] Sqrt[xr^2+t (-1+\[Rho]^2)];
{{1,0,(-B-A)/t},{1,0,(-B+A)/t}}]
]


(* ::Input::Initialization:: *)
\[CapitalDelta]2CE[\[Lambda]_,\[Alpha]_]:=(*\[Lambda]^2+(1+1/\[Alpha]^2)\[Lambda]+1/\[Alpha]^2*)1+\[Lambda](1+\[Alpha]^2 (1+\[Lambda]))
(*Second order discriminant <0 => crossing lines (degenerate hyperbola), =0 => parallel lines, >0 => point (ignore)*)


(* ::Input::Initialization:: *)
CEIntersections[\[Rho]_,\[Alpha]_,{xr_,yr_}]:=(*Calculate intersection points between \[Alpha]-ellipse and \[Rho]-circle centered at (xr, yr)*)
Module[{tol=1*^-12,\[Lambda]d,degenerate},
\[Lambda]d=Find\[Lambda]d[\[Rho],\[Alpha],{xr,yr}];(*find \[Lambda]s resulting in degenerate curves, sorted by Subscript[\[CapitalDelta], 2]*)
degenerate=If[Abs[\[CapitalDelta]2CE[\[Lambda]d[[1]],\[Alpha]]]<tol(*if Subscript[\[CapitalDelta], 2] is sufficiently close to zero, then assume parallel lines*),parallel[\[Rho],\[Alpha],{xr,yr}],hyperbola[\[Alpha],{xr,yr},\[Lambda]d[[1]]]];
Flatten[LEintersection[#,\[Alpha]]&/@degenerate,1]
]


(* ::Input::Initialization:: *)
atan[x_,y_]:=If[x==0&&y==0,0,With[{at=ArcTan[x,y]}, If[at<0,at+2\[Pi],at]]](*Atan2 from 0 to 2\[Pi]*)


(* ::Input::Initialization:: *)
sortIntersections[solutions_,{x0_,y0_}]:=Module[{s={{#[[1]],#[[2]]},atan[#[[1]]-x0,#[[2]]-y0]}&/@solutions},
Sort[s,#1[[2]]<#2[[2]]&](*Sort by \[Rho] angles 0 to 2\[Pi]*)
]


(* ::Input::Initialization:: *)
discriminant[{x_,y_},\[Alpha]_,{x0_,y0_}]:=((x-x0 )y \[Alpha]^2-x (y-y0))/Sqrt[ x^2+(y \[Alpha]^2)^2]


(* ::Input::Initialization:: *)
ellipseSegment[{x1_,y1_},{x2_,y2_},\[Alpha]_]:=Module[{\[Theta]=atan[x2,\[Alpha] y2]-atan[x1,\[Alpha] y1]},\[Theta]+=If[\[Theta]<0,2\[Pi],0];\[Alpha]/2 (\[Theta]-Sin[\[Theta]])(*Circular segment radius = 1 area times \[Alpha]*)]


(* ::Input::Initialization:: *)
circleSegment[\[Theta]1_,\[Theta]2_,\[Rho]_]:=With[{\[Theta]=\[Theta]2-\[Theta]1},\[Rho]^2/2 (\[Theta]-Sin[\[Theta]])]


(* ::Input::Initialization:: *)
fEllipse[\[Rho]_,\[Alpha]_,{x0_,y0_}]:=(*Conditional PDF of \[Rho] given response location r=(Subscript[x, 0],Subscript[y, 0]) for a given \[Alpha]-ellipse*)
If[\[Alpha]==1,f[\[Rho],Norm[{x0,y0}]],
Module[{r0={x0,y0},s=CEIntersections[\[Rho],\[Alpha],{x0,y0}],d,start,n,t},
n=s//Length;
If[n==0,If[\[Alpha] Sqrt[1-y0^2]-x0<=\[Rho],0,(2\[Rho])/\[Alpha]],
s=sortIntersections[s,r0];
d=discriminant[#[[1]],\[Alpha],r0]&/@s;
start=Ordering[Abs[d],-1][[1]];(*Choose the index of largest discriminant; use this one to begin arc/area summation*)
Do[s[[i,2]]+=2\[Pi],{i,1,start-1}]; (*Correct angles so summation works*)
t=Sum[s[[Mod[i+1,n]+1,2]]-s[[Mod[i,n]+1,2]],{i,start-1,start+n-3,2}];
\[Rho]/(\[Pi] \[Alpha]) If[d[[start]]>0,2\[Pi]-t,t]
]
]
]


(* ::Input::Initialization:: *)
FEllipse[\[Rho]_,\[Alpha]_,{x0_,y0_}]:=
If[\[Alpha]==1,F[\[Rho],Norm[{x0,y0}]],
Module[{r0={x0,y0},s=CEIntersections[\[Rho],\[Alpha],{x0,y0}],d,start,n,v},
n=s//Length;
If[n==0,If[\[Alpha] Sqrt[1-y0^2]-x0<=\[Rho],1,\[Rho]^2/\[Alpha]],
s=sortIntersections[s,r0];
d=discriminant[#[[1]],\[Alpha],r0]&/@s;
start=Ordering[Abs[d],-1][[1]];(*Choose the index of largest discriminant; use this one to begin arc/area summation*)
Do[s[[i,2]]+=2\[Pi],{i,1,start-1}]; (*Correct angles so differences works*)
v=Sum[ellipseSegment[s[[Mod[i,n]+1,1]],s[[Mod[i+1,n]+1,1]],\[Alpha]]-circleSegment[s[[Mod[i,n]+1,2]],s[[Mod[i+1,n]+1,2]],\[Rho]],{i,start-1,start+n-3,2}];
If[d[[start]]>0,\[Pi] \[Rho]^2+v,\[Pi] \[Alpha]-v]/(\[Pi] \[Alpha])
]
]
]


(* ::Input::Initialization:: *)
f[\[Rho]_,r_]:=2\[Rho] Piecewise[
{{1,0<=\[Rho]<=1-r},
{1/\[Pi] ArcCos[(\[Rho]^2+r^2-1)/(2\[Rho] r)],1-r<\[Rho]<=1+r}},
0
]


(* ::Input::Initialization:: *)
F[\[Rho]_,r_]:=Piecewise[
{{0,\[Rho]<0},
{\[Rho]^2,0<=\[Rho]<=1-r},
{1-1/(2 \[Pi]) (Sqrt[(\[Rho]^2-(1-r)^2)((1+r)^2-\[Rho]^2)]-2 \[Rho]^2 ArcCos[(-1+r^2+\[Rho]^2)/(2r \[Rho])]+2 ArcCos[(-1+\[Rho]^2-r^2)/(2 r)]),1-r<\[Rho]<1+r}},
1]


End[];


EndPackage[];
