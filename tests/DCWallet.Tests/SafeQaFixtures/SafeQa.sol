// SPDX-License-Identifier: MIT
pragma solidity 0.8.30;
/// Isolated QA only. Hard-coded public designated test owner; no secrets or configurable authority.
contract QaSignatureOwner {
    address constant OWNER = 0xb92Bd490c8e9e5D9C46d71666418d4e7Ae4534e9;
    function valid(bytes32 digest, bytes calldata signature) internal pure returns (bool) {
        if (signature.length != 65) return false;
        bytes32 r; bytes32 s; uint8 v;
        assembly { r := calldataload(signature.offset) s := calldataload(add(signature.offset, 32)) v := byte(0, calldataload(add(signature.offset, 64))) }
        if (uint256(s) > 0x7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a0 || (v != 27 && v != 28)) return false;
        return ecrecover(digest,v,r,s) == OWNER;
    }
    function isValidSignature(bytes32 digest, bytes calldata signature) external pure returns (bytes4) {
        return valid(digest,signature) ? bytes4(0x1626ba7e) : bytes4(0xffffffff);
    }
    function isValidSignature(bytes calldata data, bytes calldata signature) external pure returns (bytes4) {
        return valid(keccak256(data),signature) ? bytes4(0x20c13b0b) : bytes4(0xffffffff);
    }
}
/// No storage writes and no generic calls. Sends exactly one QA wei back to the designated QA owner.
contract QaDelegateTarget {
    event QaDelegate(address context, address recipient);
    function payOneWei(address payable recipient) external {
        require(msg.sender == 0xb92Bd490c8e9e5D9C46d71666418d4e7Ae4534e9 && recipient == msg.sender, "QA only");
        (bool ok,) = recipient.call{value: 1}(""); require(ok,"QA payment");
        emit QaDelegate(address(this),recipient);
    }
}

