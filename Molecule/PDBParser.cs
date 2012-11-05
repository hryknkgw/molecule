using System;
using System.Linq;
using System.Collections.Generic;

namespace Molecule
{
	public class PDBParser
	{
		private System.Net.WebClient webClient = new System.Net.WebClient ();
		
		public PDBParser ()
		{
		}
		
		public Model Parse (System.IO.TextReader reader)
		{
			var model = new Model ();
			var missingResidues = new Queue<Residue> ();
			var reAtom = new System.Text.RegularExpressions.Regex ("^ATOM  (?<serial>.{5}) (?<name>.{4})(?<altLoc>.{1})(?<resName>.{3}) (?<chainID>.{1})(?<resSeq>.{4})(?<iCode>.{1})   (?<x>.{8})(?<y>.{8})(?<z>.{8})(?<occupancy>.{6})(?<tempFactor>.{6})          (?<element>.{2})(?<charge>.{2})$");
			var reRemark = new System.Text.RegularExpressions.Regex ("^REMARK (?<remarkNum>.{3})");
			var reRemark2 = new System.Text.RegularExpressions.Regex ("^REMARK   2 RESOLUTION. (?<resolution>.{7}) ANGSTROMS.");
			var reRemark465 = new System.Text.RegularExpressions.Regex ("^REMARK 465 (?<modelNo>.{3}) (?<resName>.{3}) (?<chainID>.{1})  (?<resSeq>.{4})(?<iCode>.{1})");
			while (reader.Peek() != -1) {
				var line = reader.ReadLine ();
				if (line.StartsWith ("ATOM")) {
					var m = reAtom.Match (line);
					var atom = new Atom (
						int.Parse (m.Groups ["serial"].Value),
						m.Groups ["name"].Value,
						m.Groups ["altLoc"].Value [0],
						m.Groups ["resName"].Value,
						m.Groups ["chainID"].Value [0],
						int.Parse (m.Groups ["resSeq"].Value),
						m.Groups ["iCode"].Value [0],
						double.Parse (m.Groups ["x"].Value),
						double.Parse (m.Groups ["y"].Value),
						double.Parse (m.Groups ["z"].Value),
						double.Parse (m.Groups ["occupancy"].Value),
						double.Parse (m.Groups ["tempFactor"].Value),
						m.Groups ["element"].Value,
						m.Groups ["charge"].Value
					);
					if (model.Chains.Count == 0 || !(model.Chains.Last ().ChainID == atom.ChainID)) {
						while (missingResidues.Count() > 0 && model.Chains.Count != 0 && missingResidues.First().ChainID == model.Chains.Last ().ChainID) {
							model.Residues.Add (missingResidues.Dequeue ());
						}
						var chain = new Chain (atom.ChainID);
						model.Chains.Add (chain);
					}
					atom.Chain = model.Chains.Last ();
					atom.Chain.Atoms.Add (atom);
					if (model.Residues.Count == 0 || !(model.Residues.Last ().ChainID == atom.ChainID && model.Residues.Last ().ResSeq == atom.ResSeq && model.Residues.Last ().ICode == atom.ICode)) {
						var residue = new Residue (
							atom.ResName,
							atom.ChainID,
							atom.ResSeq,
							atom.ICode
						);
						while (missingResidues.Count() > 0 && missingResidues.First().ChainID == residue.ChainID && (missingResidues.First().ResSeq < residue.ResSeq || (missingResidues.First().ResSeq == residue.ResSeq && missingResidues.First().ICode < residue.ICode))) {
							model.Residues.Add (missingResidues.Dequeue ());
						}
						model.Residues.Add (residue);
					}
					atom.Residue = model.Residues.Last ();
					atom.Residue.Atoms.Add (atom);
					model.Atoms.Add (atom);
				} else if (line.StartsWith ("REMARK")) {
					var m = reRemark.Match (line);
					if (m.Success) {
						int remarkNum = int.Parse (m.Groups ["remarkNum"].Value);
						switch (remarkNum) {
						case 2:
							m = reRemark2.Match (line);
							if (m.Success) {
								model.Resolution = double.Parse (m.Groups ["resolution"].Value);
							}
							break;
						case 3:
							double rfree;
							if (double.TryParse (line.Replace ("REMARK   3   FREE R VALUE                     :", ""), out rfree)) {
								model.RFree = rfree;
							}
							break;
						case 465:
							m = reRemark465.Match (line);
							int resSeq;
							if (m.Success && int.TryParse (m.Groups ["resSeq"].Value, out resSeq)) {
								var missingResidue = new Residue (
									m.Groups ["resName"].Value, 
									m.Groups ["chainID"].Value [0], 
									resSeq, 
									m.Groups ["iCode"].Value [0]
								);
								missingResidues.Enqueue (missingResidue);
							}
							break;
						default:
							break;
						}
					}
				}
			}
			while (missingResidues.Count() > 0 && model.Chains.Count != 0 && missingResidues.First().ChainID == model.Chains.Last ().ChainID) {
				model.Residues.Add (missingResidues.Dequeue ());
			}
			return model;
		}
		
		public Model Parse (string path)
		{
			System.Diagnostics.Trace.TraceInformation (path);
			if (Uri.IsWellFormedUriString (path, UriKind.Absolute)) {
				return Parse (new Uri (path));
			} else {
				using (var reader = new System.IO.StreamReader(path)) {
					return Parse (reader);
				}
			}
		}
		
		public Model Parse (Uri address)
		{
			using (var reader = new System.IO.StreamReader(new System.IO.Compression.GZipStream(webClient.OpenRead(address), System.IO.Compression.CompressionMode.Decompress))) {
				return Parse (reader);
			}
		}
	}
}
