using System;
using System.Numerics;
using Solnet.Wallet;

Console.WriteLine("Paste base64 private key from DB:");
Console.Write("Base64> ");
var input = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(input)) return;

Console.Write("Public key: ");
var pubKeyStr = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(pubKeyStr)) return;

var privateKeyBytes = Convert.FromBase64String(input);
var pubkey = new PublicKey(pubKeyStr);
var account = new Account(privateKeyBytes, pubkey.KeyBytes);

// 64-byte keypair = private (32) + public (32)
var keypair64 = new byte[64];
Array.Copy(account.PrivateKey.KeyBytes, 0, keypair64, 0, 32);
Array.Copy(pubkey.KeyBytes, 0, keypair64, 32, 32);

Console.WriteLine();
Console.WriteLine($"Public key:        {pubkey.Key}");
Console.WriteLine($"Private key (b58): {ToBase58(keypair64)}");
Console.ReadLine();

static string ToBase58(byte[] data)
{
    const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    var intData = new BigInteger(data, isUnsigned: true, isBigEndian: true);
    var result = "";
    while (intData > 0) { intData = BigInteger.DivRem(intData, 58, out var rem); result = alphabet[(int)rem] + result; }
    foreach (var b in data) { if (b == 0) result = "1" + result; else break; }
    return result;
}
