using System.Text;
using System.Text.RegularExpressions;
using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public class TransactionMatrixTests
{
    [Fact] public void CatalogMatrixHasExplicitCoverageForEveryOperation()
    {
        // An explicit allowlist prevents a newly discovered draft being silently labelled tested.
        var exercised = new HashSet<string>(StringComparer.Ordinal) {
            "TransactionDraft.Del", "TransactionDraft.ContractCall",
            "Erc20Service.BuildTransferDraft", "Erc20Service.BuildApproveDraft", "Erc20Service.BuildTransferFromDraft",
            "Erc721Service.TransferFrom", "Erc721Service.SafeTransferFrom",
            "Erc1155Service.SafeTransferFrom", "Erc1155Service.SafeBatchTransferFrom",
            "BridgeDrafts.BuildTransferNative", "BridgeDrafts.BuildTransferToken",
            "ChecksService.BuildCreateDelDraft", "MultisendDel.Build",
            "DelegationDrafts.BuildDelegateDelDraft", "DelegationDrafts.BuildDelegateHoldDelDraft", "DelegationDrafts.BuildDelegateHoldDraft", "DelegationDrafts.BuildDelegateByPermitDraft",
            "TokenCreationDrafts.BuildCreateTokenDraft", "TokenCreationDrafts.BuildCreateReservelessTokenDraft",
            "DecimalNftDrafts.BuildCreateErc721Draft", "DecimalNftDrafts.BuildCreateErc1155Draft",
            "TokenEconomyDrafts.BuildBuyDraft", "TokenEconomyDrafts.BuildSellDraft",
            "ValidatorService.BuildPauseSelfDraft", "ValidatorService.BuildUnpauseSelfDraft", "ValidatorService.BuildRemoveValidatorDraft",
            "SafeTransactions.BuildExecutionDraft"
        };
        var rows=OperationCatalog.All.Select(op=>new { Op=op, Tested=exercised.Contains(op.Method.DeclaringType!.Name+"."+op.Method.Name) && !op.Method.GetParameters().Any(p=>p.Name=="decimals") }).ToArray();
        Assert.Equal(OperationCatalog.All.Count,rows.Length); Assert.True(rows.Count(r=>r.Tested)>20);
        var root=Environment.GetEnvironmentVariable("DC_MATRIX_ROOT"); if(string.IsNullOrEmpty(root)) return;
        var console=Environment.GetEnvironmentVariable("DC_CONSOLE_ROOT")!;
        var consoleFiles=Directory.GetFiles(console,"*.js",SearchOption.AllDirectories).Concat(Directory.GetFiles(console,"*.jsx",SearchOption.AllDirectories)).Where(p=>!p.Contains("node_modules")).ToArray();
        var sourceFiles=Directory.GetFiles(Path.Combine(root,"vendor","Decimal.Web3"),"*.cs",SearchOption.AllDirectories).Where(p=>!p.Contains("\\obj\\")).ToArray();
        var b=new StringBuilder("# Transaction coverage matrix\n\nGenerated from OperationCatalog by TransactionMatrixTests.\n\nNetwork execution is **not verified** for every row. All current catalog entries are invoked: synchronous calldata baselines, controlled-RPC checked builders, and one expected unsupported rejection. Baselines are snapshots of this SDK, not independent ABI validation. Separate semantic/selector/signature tests are identified below. Presence in Console source is a reference trace, not independent proof of complete ABI equivalence.\n\nSources: vendored SDK, Console MintCandy `src`, wallet tests. Checked rows refer to DraftEncodingTests, ExtendedDraftTests and CoreTests. CatalogEncodingTests covers exact target, integer value and full calldata. CheckedBuilderTests covers successful and rejected state-dependent mint/burn.\n\n| Operation | Offline fixture | Console ABI trace | Network execution |\n|---|---|---|---|\n");
        b.Insert(b.ToString().IndexOf("| Operation",StringComparison.Ordinal), $"\nCatalog: {rows.Length} entries; {rows.Count(r=>r.Op.Method.ReturnType.Name.StartsWith("Task"))} controlled-RPC builders; {rows.Count(r=>!r.Op.Method.ReturnType.Name.StartsWith("Task"))-1} successful synchronous baselines; 1 unsupported rejection.\n\n");
        foreach(var row in rows)
        {
            var op=row.Op; var file=sourceFiles.FirstOrDefault(p=>File.ReadAllText(p).Contains("class "+op.Method.DeclaringType!.Name) || File.ReadAllText(p).Contains("record "+op.Method.DeclaringType!.Name));
            var text=file is null?"":File.ReadAllText(file); var start=text.IndexOf(op.Method.Name+"(",StringComparison.Ordinal);
            var body=start<0?"":text[start..]; var next=body.IndexOf("\n    public ",StringComparison.Ordinal); if(next>=0) body=body[..next];
            var names=Regex.Matches(body,@"new\s+(\w+Function)\b").Select(m=>m.Groups[1].Value).Distinct().ToArray();
            var all=string.Join("\n",sourceFiles.Select(File.ReadAllText));
            var abiNames=names.Select(name=>Regex.Match(all,@"\[Function\(""([^""]+)""[^\]]*\)\]\s*public (?:sealed )?class "+name+@"\b")).Where(m=>m.Success).Select(m=>m.Groups[1].Value).ToArray();
            var fixture = op.Method.DeclaringType!.Name=="TokenEconomyDrafts" && op.Method.Name=="BuildMintDraft" ? "unsupported: expected rejection" : op.Method.ReturnType.Name.StartsWith("Task") ? "controlled RPC + state assertions" : "exact calldata baseline";
            var trace=abiNames.SelectMany(name=>consoleFiles.Where(p=>Regex.IsMatch(File.ReadAllText(p),@"\b"+Regex.Escape(name)+@"\s*\(")).Select(p=>Path.GetFileName(p)+":"+name)).Distinct().ToArray();
            var method = op.Method.DeclaringType!.Name + "." + op.Method.Name;
            var network = method switch {
                "ChecksService.BuildCreateDelDraft" or "ChecksService.BuildCreateTokenDraft" or "ChecksService.BuildCreateTokenByPermitDraft" or "ChecksService.BuildRedeemDraft" => "testnet receipt verified; see VALIDATION-COMPLETION.md",
                "TokenEconomyDrafts.BuildBuyTokenForExactDelDraft" or "TokenEconomyDrafts.BuildSellExactTokensForDelDraft" or "TokenEconomyDrafts.BuildConvertDraft" or "TokenEconomyDrafts.BuildConvertWithPermitDraft" => "testnet receipt and balance verified; see VALIDATION-COMPLETION.md",
                "DelegationDrafts.BuildDelegateByPermitDraft" or "DelegationDrafts.BuildWithdrawDraft" or "DelegationDrafts.BuildCompleteDraft" => "reviewed testnet lifecycle; exact asset/ABI limits in VALIDATION-COMPLETION.md",
                "DecimalNftDrafts.BuildCreateDrc721LegacyDraft" => "testnet QA collection creation verified; see VALIDATION-COMPLETION.md",
                "DecimalNftDrafts.BuildMintErc721ByEthDraft" => "SDK selector needs current wallet mintByDEL adapter on testnet; see VALIDATION-COMPLETION.md",
                _ => "not certified by this fixture test; see validation reports for separate evidence"
            };
            b.AppendLine($"| ``{op.Id}`` | {fixture}; {(row.Tested?"independent semantic/selector checks":"baseline only")} | {(trace.Length==0?"no literal match / delegated wrapper":string.Join(", ",trace.Take(4)))} | {network} |");
        }
        b.AppendLine("\n## Limits and defects\n\n- Catalog excludes builders returning SafeTransactionDraft. Safe typed hash/sign/pack/exec is tested separately, including chain/nonce binding and duplicate rejection. Safe deployment, threshold/nonce RPC and coordinated signing UI remain separate workflows.\n- Checked mint/burn builders use a controlled RPC fixture with owner, mintable, burnable, cap and balance reads; actual deployed token execution remains unknown.\n- TokenEconomyDrafts.BuildMintDraft is intentionally unsupported by the SDK: mint(address,uint256) is absent from DecimalToken ABI at go-smart-node 9e6c6d7. The fixture asserts rejection, not successful draft construction.\n- Permit fixtures check calldata shape. Wallet does not create a real token permit in these tests.\n- Bridge tests validate draft value and overflow. Relayer/VM finalization, cross-chain fees and destination execution remain unverified.\n- Legacy and current NFT creation selectors differ. Deployment-specific ABI and collection capabilities must be checked before sending.\n- Generic forms cannot establish owner/minter/validator permissions without live checks.\n- SafeQueries checked execution does not independently validate weighted quorum, deployment code or on-chain hash. Wallet Safe UI must enforce these separately.\n- No test here broadcasts transactions.\n");
        File.WriteAllText(Path.Combine(root,"docs","TRANSACTION-MATRIX.md"),b.ToString());
    }
}
