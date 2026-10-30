"""Negative fixtures for narrow historical-image identity reuse, no network."""
import copy
import tempfile
import unittest
from pathlib import Path
from quest_public_proof import PREFIX, REUSED, git_blob, inherited_row, sha

class ImageReuseProofTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix="firstgame-quest-proof-")
        self.root=Path(self.temp.name)
        self.name=PREFIX+"fixture.jpg"
        self.path=self.root/self.name
        self.path.parent.mkdir(parents=True)
        self.path.write_bytes(b"original-fixture-jpeg-bytes")
        self.digest=sha(self.path)
        self.row={"file":self.name,"passed":True,"status":200,"sha256":self.digest,"expectedSha256":self.digest,"bytes":self.path.stat().st_size,"expectedBytes":self.path.stat().st_size,"checkedUtc":"2026-10-29T00:00:00+00:00"}
        self.prior={self.name:self.row}
        self.tree={"files":{self.name:{"gitBlob":git_blob(self.path),"mode":"100644"}}}
        self.ref={"sha256":"a"*64}
    def tearDown(self):self.temp.cleanup()
    def prove(self,tree=None):
        return inherited_row(self.name,self.digest,self.root,self.prior,self.tree,tree or self.tree,self.ref)
    def test_retains_historical_timestamp_and_explicit_nonfresh_status(self):
        row=self.prove()
        self.assertEqual(row["checkedUtc"],self.row["checkedUtc"])
        self.assertFalse(row["freshGet"])
        self.assertEqual(row["verificationSource"],REUSED)
        self.assertEqual(row["inheritedGet"]["auditSha256"],self.ref["sha256"])
    def test_different_remote_git_blob_rejected(self):
        tree=copy.deepcopy(self.tree);tree["files"][self.name]["gitBlob"]="0"*40
        with self.assertRaisesRegex(ValueError,"objects differ"):self.prove(tree)
    def test_changed_local_bytes_rejected_even_when_size_equal(self):
        self.path.write_bytes(b"x"*self.path.stat().st_size)
        with self.assertRaisesRegex(ValueError,"objects differ"):self.prove()
    def test_failed_prior_get_rejected(self):
        self.row["status"]=404
        with self.assertRaisesRegex(ValueError,"prior actual GET"):self.prove()
    def test_runtime_binary_cannot_reuse_image_proof(self):
        with self.assertRaisesRegex(ValueError,"Only unchanged original"):
            inherited_row("games/fantasy-cards/Build/game.wasm","0"*64,self.root,{},self.tree,self.tree,self.ref)
    def test_missing_deployed_entry_rejected(self):
        with self.assertRaisesRegex(ValueError,"objects differ"):self.prove({"files":{}})

if __name__=="__main__":unittest.main()
