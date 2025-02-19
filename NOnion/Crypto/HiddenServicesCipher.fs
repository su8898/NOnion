﻿namespace NOnion.Crypto

open System.Text

open Org.BouncyCastle.Crypto.Agreement
open Org.BouncyCastle.Crypto.Digests
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Signers
open Chaos.NaCl

open NOnion
open NOnion.Utility

module HiddenServicesCipher =
    let CalculateMacWithSHA3256 (key: array<byte>) (msg: array<byte>) =
        let data =
            let keyLenBytes =
                key.LongLength
                |> uint64
                |> IntegerSerialization.FromUInt64ToBigEndianByteArray

            Array.concat [ keyLenBytes; key; msg ]

        let digestEngine = Sha3Digest ()
        let output = Array.zeroCreate (digestEngine.GetDigestSize ())
        digestEngine.BlockUpdate (data, 0, data.Length)
        digestEngine.DoFinal (output, 0) |> ignore<int>

        output

    let SignWithED25519
        (privateKey: Ed25519PrivateKeyParameters)
        (data: array<byte>)
        =
        let signer = Ed25519Signer ()
        signer.Init (true, privateKey)
        signer.BlockUpdate (data, 0, data.Length)
        signer.GenerateSignature ()

    let CalculateShake256 (length: int) (data: array<byte>) =
        let digestEngine = ShakeDigest 256
        let output = Array.zeroCreate length

        digestEngine.BlockUpdate (data, 0, data.Length)
        digestEngine.DoFinal (output, 0) |> ignore<int>
        output

    let CalculateBlindingFactor
        (periodNumber: uint64)
        (periodLength: uint64)
        (publicKey: array<byte>)
        =
        let nonce =
            Array.concat
                [
                    "key-blind" |> Encoding.ASCII.GetBytes
                    periodNumber
                    |> IntegerSerialization.FromUInt64ToBigEndianByteArray
                    periodLength
                    |> IntegerSerialization.FromUInt64ToBigEndianByteArray
                ]

        let digestEngine = Sha3Digest ()
        let output = Array.zeroCreate (digestEngine.GetDigestSize ())

        digestEngine.BlockUpdate (
            Constants.HiddenServiceBlindString,
            0,
            Constants.HiddenServiceBlindString.Length
        )

        digestEngine.BlockUpdate (publicKey, 0, publicKey.Length)

        digestEngine.BlockUpdate (
            Constants.Ed25519BasePointString,
            0,
            Constants.Ed25519BasePointString.Length
        )

        digestEngine.BlockUpdate (nonce, 0, nonce.Length)
        digestEngine.DoFinal (output, 0) |> ignore<int>

        //CLAMPING
        output.[0] <- output.[0] &&& 248uy
        output.[31] <- output.[31] &&& 63uy
        output.[31] <- output.[31] ||| 64uy

        output

    let CalculateBlindedPublicKey
        (blindingFactor: array<byte>)
        (publicKey: array<byte>)
        =

        blindingFactor.[0] <- blindingFactor.[0] &&& 248uy
        blindingFactor.[31] <- blindingFactor.[31] &&& 63uy
        blindingFactor.[31] <- blindingFactor.[31] ||| 64uy

        match Ed25519.CalculateBlindedPublicKey (publicKey, blindingFactor) with
        | true, output -> output
        | false, _ -> failwith "can't calculate blinded public key"

    let BuildBlindedPublicKey
        (periodNumber: uint64, periodLength: uint64)
        (publicKey: array<byte>)
        =
        let blindingFactor =
            CalculateBlindingFactor periodNumber periodLength publicKey

        CalculateBlindedPublicKey blindingFactor publicKey

    let private GetSubCredential
        (periodInfo: uint64 * uint64)
        (publicKey: array<byte>)
        =
        let digestEngine = Sha3Digest ()

        let credential = digestEngine.GetByteLength () |> Array.zeroCreate

        let credentialDigestInput =
            Array.concat
                [
                    "credential" |> Encoding.ASCII.GetBytes
                    publicKey
                ]

        digestEngine.BlockUpdate (
            credentialDigestInput,
            0,
            credentialDigestInput.Length
        )

        digestEngine.DoFinal (credential, 0) |> ignore<int>

        let blindedKey = BuildBlindedPublicKey periodInfo publicKey

        let subcredentialDigestInput =
            Array.concat
                [
                    "subcredential" |> Encoding.ASCII.GetBytes
                    credential
                    blindedKey
                ]

        let subcredential = digestEngine.GetByteLength () |> Array.zeroCreate

        digestEngine.BlockUpdate (
            subcredentialDigestInput,
            0,
            subcredentialDigestInput.Length
        )

        digestEngine.DoFinal (subcredential, 0) |> ignore<int>

        subcredential

    let EncryptIntroductionData
        (data: array<byte>)
        (randomClientPrivateKey: X25519PrivateKeyParameters)
        (randomClientPublicKey: X25519PublicKeyParameters)
        (introAuthPublicKey: Ed25519PublicKeyParameters)
        (introEncPublicKey: X25519PublicKeyParameters)
        (periodInfo: uint64 * uint64)
        (masterPubKey: array<byte>)
        =
        let keyAgreement = X25519Agreement ()

        keyAgreement.Init randomClientPrivateKey

        let sharedSecret = Array.zeroCreate keyAgreement.AgreementSize
        keyAgreement.CalculateAgreement (introEncPublicKey, sharedSecret, 0)

        let subcredential = GetSubCredential periodInfo masterPubKey

        let introSecretHsInput =
            Array.concat
                [
                    sharedSecret
                    introAuthPublicKey.GetEncoded ()
                    randomClientPublicKey.GetEncoded ()
                    introEncPublicKey.GetEncoded ()
                    Constants.HiddenServiceNTor.ProtoId
                ]

        let info =
            Array.concat
                [
                    Constants.HiddenServiceNTor.MExpand
                    subcredential
                ]

        let finalDigestInput =
            Array.concat
                [
                    introSecretHsInput
                    Constants.HiddenServiceNTor.TEncrypt
                    info
                ]

        let hsKeys = CalculateShake256 64 finalDigestInput

        let encKey = hsKeys |> Array.take 32
        let macKey = hsKeys |> Array.skip 32 |> Array.take 32

        let cipher = TorStreamCipher (encKey, None)
        let digest = data |> CalculateMacWithSHA3256 macKey

        let encryptedInnerData = data |> cipher.Encrypt

        (encryptedInnerData, digest)

    let DecryptIntroductionData
        (encryptedData: array<byte>)
        (clientRandomKey: X25519PublicKeyParameters)
        (introAuthPublicKey: Ed25519PublicKeyParameters)
        (introEncPrivateKey: X25519PrivateKeyParameters)
        (introEncPublicKey: X25519PublicKeyParameters)
        (periodInfo: uint64 * uint64)
        (masterPubKey: array<byte>)
        =
        let keyAgreement = X25519Agreement ()
        keyAgreement.Init introEncPrivateKey

        let sharedSecret = Array.zeroCreate keyAgreement.AgreementSize
        keyAgreement.CalculateAgreement (clientRandomKey, sharedSecret, 0)

        let subcredential = GetSubCredential periodInfo masterPubKey

        let introSecretHsInput =
            Array.concat
                [
                    sharedSecret
                    introAuthPublicKey.GetEncoded ()
                    clientRandomKey.GetEncoded ()
                    introEncPublicKey.GetEncoded ()
                    Constants.HiddenServiceNTor.ProtoId
                ]

        let info =
            Array.concat
                [
                    Constants.HiddenServiceNTor.MExpand
                    subcredential
                ]

        let finalDigestInput =
            Array.concat
                [
                    introSecretHsInput
                    Constants.HiddenServiceNTor.TEncrypt
                    info
                ]

        let hsKeys = CalculateShake256 64 finalDigestInput

        let encKey = hsKeys |> Array.take Constants.KeyS256Length

        let macKey =
            hsKeys
            |> Array.skip Constants.KeyS256Length
            |> Array.take Constants.Digest256Length

        let cipher = TorStreamCipher (encKey, None)
        let decryptedData = encryptedData |> cipher.Encrypt

        let digest = decryptedData |> CalculateMacWithSHA3256 macKey

        (decryptedData, digest)

    let CalculateServerRendezvousKeys
        (clientPublicKey: X25519PublicKeyParameters)
        (serverRandomPublicKey: X25519PublicKeyParameters)
        (serverRandomPrivateKey: X25519PrivateKeyParameters)
        (introAuthPublicKey: Ed25519PublicKeyParameters)
        (introEncPrivateKey: X25519PrivateKeyParameters)
        (introEncPublicKey: X25519PublicKeyParameters)
        =
        let keyAgreementY, keyAgreementB =
            X25519Agreement (), X25519Agreement ()

        keyAgreementY.Init serverRandomPrivateKey
        keyAgreementB.Init introEncPrivateKey

        let sharedSecretXy, sharedSecretXb =
            Array.zeroCreate keyAgreementY.AgreementSize,
            Array.zeroCreate keyAgreementB.AgreementSize

        keyAgreementY.CalculateAgreement (clientPublicKey, sharedSecretXy, 0)
        keyAgreementB.CalculateAgreement (clientPublicKey, sharedSecretXb, 0)

        let rendSecretHsInput =
            Array.concat
                [
                    sharedSecretXy
                    sharedSecretXb
                    introAuthPublicKey.GetEncoded ()
                    introEncPublicKey.GetEncoded ()
                    clientPublicKey.GetEncoded ()
                    serverRandomPublicKey.GetEncoded ()
                    Constants.HiddenServiceNTor.ProtoId
                ]

        let ntorKeySeed =
            CalculateMacWithSHA3256
                rendSecretHsInput
                Constants.HiddenServiceNTor.TEncrypt

        let verify =
            CalculateMacWithSHA3256
                rendSecretHsInput
                Constants.HiddenServiceNTor.TVerify

        let authInput =
            Array.concat
                [
                    verify
                    introAuthPublicKey.GetEncoded ()
                    introEncPublicKey.GetEncoded ()
                    serverRandomPublicKey.GetEncoded ()
                    clientPublicKey.GetEncoded ()
                    Constants.HiddenServiceNTor.AuthInputSuffix
                ]

        let authInputMac =
            CalculateMacWithSHA3256 authInput Constants.HiddenServiceNTor.TMac

        (ntorKeySeed, authInputMac)

    let CalculateClientRendezvousKeys
        (serverPublicKey: X25519PublicKeyParameters)
        (clientPublicKey: X25519PublicKeyParameters)
        (clientPrivateKey: X25519PrivateKeyParameters)
        (introAuthPublicKey: Ed25519PublicKeyParameters)
        (introEncPublicKey: X25519PublicKeyParameters)
        =
        let keyAgreement = X25519Agreement ()

        keyAgreement.Init clientPrivateKey

        let sharedSecretXy, sharedSecretXb =
            Array.zeroCreate keyAgreement.AgreementSize,
            Array.zeroCreate keyAgreement.AgreementSize

        keyAgreement.CalculateAgreement (serverPublicKey, sharedSecretXy, 0)
        keyAgreement.CalculateAgreement (introEncPublicKey, sharedSecretXb, 0)

        let rendSecretHsInput =
            Array.concat
                [
                    sharedSecretXy
                    sharedSecretXb
                    introAuthPublicKey.GetEncoded ()
                    introEncPublicKey.GetEncoded ()
                    clientPublicKey.GetEncoded ()
                    serverPublicKey.GetEncoded ()
                    Constants.HiddenServiceNTor.ProtoId
                ]

        let ntorKeySeed =
            CalculateMacWithSHA3256
                rendSecretHsInput
                Constants.HiddenServiceNTor.TEncrypt

        let verify =
            CalculateMacWithSHA3256
                rendSecretHsInput
                Constants.HiddenServiceNTor.TVerify

        let authInput =
            Array.concat
                [
                    verify
                    introAuthPublicKey.GetEncoded ()
                    introEncPublicKey.GetEncoded ()
                    serverPublicKey.GetEncoded ()
                    clientPublicKey.GetEncoded ()
                    Constants.HiddenServiceNTor.AuthInputSuffix
                ]

        let authInputMac =
            CalculateMacWithSHA3256 authInput Constants.HiddenServiceNTor.TMac

        (ntorKeySeed, authInputMac)
