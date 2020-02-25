// Copyright 2018, Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Apis.Util;

namespace FirebaseAdmin.Auth
{
    /// <summary>
    /// This is the entry point to all server-side Firebase Authentication operations. You can
    /// get an instance of this class via <c>FirebaseAuth.DefaultInstance</c>.
    /// </summary>
    public sealed class FirebaseAuth : IFirebaseService
    {
        private readonly Lazy<FirebaseTokenFactory> tokenFactory;
        private readonly Lazy<FirebaseTokenVerifier> idTokenVerifier;
        private readonly Lazy<FirebaseUserManager> userManager;
        private readonly object authLock = new object();
        private bool deleted;

        internal FirebaseAuth(FirebaseAuthArgs args)
        {
            args.ThrowIfNull(nameof(args));
            this.tokenFactory = args.TokenFactory.ThrowIfNull(nameof(args.TokenFactory));
            this.idTokenVerifier = args.IdTokenVerifier.ThrowIfNull(nameof(args.IdTokenVerifier));
            this.userManager = args.UserManager.ThrowIfNull(nameof(args.UserManager));
        }

        /// <summary>
        /// Gets the auth instance associated with the default Firebase app. This property is
        /// <c>null</c> if the default app doesn't yet exist.
        /// </summary>
        public static FirebaseAuth DefaultInstance
        {
            get
            {
                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                {
                    return null;
                }

                return GetAuth(app);
            }
        }

        /// <summary>
        /// Returns the auth instance for the specified app.
        /// </summary>
        /// <returns>The <see cref="FirebaseAuth"/> instance associated with the specified
        /// app.</returns>
        /// <exception cref="System.ArgumentNullException">If the app argument is null.</exception>
        /// <param name="app">An app instance.</param>
        public static FirebaseAuth GetAuth(FirebaseApp app)
        {
            if (app == null)
            {
                throw new ArgumentNullException("App argument must not be null.");
            }

            return app.GetOrInit<FirebaseAuth>(typeof(FirebaseAuth).Name, () =>
            {
                return new FirebaseAuth(FirebaseAuthArgs.Create(app));
            });
        }

        /// <summary>
        /// Creates a Firebase custom token for the given user ID. This token can then be sent
        /// back to a client application to be used with the
        /// <a href="https://firebase.google.com/docs/auth/admin/create-custom-tokens#sign_in_using_custom_tokens_on_clients">
        /// signInWithCustomToken</a> authentication API.
        /// <para>
        /// This method attempts to generate a token using:
        /// <list type="number">
        /// <item>
        /// <description>the private key of <see cref="FirebaseApp"/>'s service account
        /// credentials, if provided at initialization.</description>
        /// </item>
        /// <item>
        /// <description>the IAM service if a service accound ID was specified via
        /// <see cref="AppOptions"/></description>
        /// </item>
        /// <item>
        /// <description>the local metadata server if the code is deployed in a GCP-managed
        /// environment.</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <returns>A task that completes with a Firebase custom token.</returns>
        /// <exception cref="ArgumentException">If <paramref name="uid"/> is null, empty or longer
        /// than 128 characters.</exception>
        /// <exception cref="InvalidOperationException">If no service account can be discovered
        /// from either the <see cref="AppOptions"/> or the deployment environment.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while creating a custom
        /// token.</exception>
        /// <param name="uid">The UID to store in the token. This identifies the user to other
        /// Firebase services (Realtime Database, Firebase Auth, etc.). Must not be longer than
        /// 128 characters.</param>
        public async Task<string> CreateCustomTokenAsync(string uid)
        {
            return await this.CreateCustomTokenAsync(uid, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Firebase custom token for the given user ID. This token can then be sent
        /// back to a client application to be used with the
        /// <a href="https://firebase.google.com/docs/auth/admin/create-custom-tokens#sign_in_using_custom_tokens_on_clients">
        /// signInWithCustomToken</a> authentication API.
        /// <para>
        /// This method attempts to generate a token using:
        /// <list type="number">
        /// <item>
        /// <description>the private key of <see cref="FirebaseApp"/>'s service account
        /// credentials, if provided at initialization.</description>
        /// </item>
        /// <item>
        /// <description>the IAM service if a service accound ID was specified via
        /// <see cref="AppOptions"/></description>
        /// </item>
        /// <item>
        /// <description>the local metadata server if the code is deployed in a GCP-managed
        /// environment.</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <returns>A task that completes with a Firebase custom token.</returns>
        /// <exception cref="ArgumentException">If <paramref name="uid"/> is null, empty or longer
        /// than 128 characters.</exception>
        /// <exception cref="InvalidOperationException">If no service account can be discovered
        /// from either the <see cref="AppOptions"/> or the deployment environment.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while creating a custom
        /// token.</exception>
        /// <param name="uid">The UID to store in the token. This identifies the user to other
        /// Firebase services (Realtime Database, Firebase Auth, etc.). Must not be longer than
        /// 128 characters.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        public async Task<string> CreateCustomTokenAsync(
            string uid, CancellationToken cancellationToken)
        {
            return await this.CreateCustomTokenAsync(uid, null, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Firebase custom token for the given user ID containing the specified
        /// additional claims. This token can then be sent back to a client application to be used
        /// with the
        /// <a href="https://firebase.google.com/docs/auth/admin/create-custom-tokens#sign_in_using_custom_tokens_on_clients">
        /// signInWithCustomToken</a> authentication API.
        /// <para>This method uses the same mechanisms as
        /// <see cref="CreateCustomTokenAsync(string)"/> to sign custom tokens.</para>
        /// </summary>
        /// <returns>A task that completes with a Firebase custom token.</returns>
        /// <exception cref="ArgumentException">If <paramref name="uid"/> is null, empty or longer
        /// than 128 characters. Or, if <paramref name="developerClaims"/> contains any standard
        /// JWT claims.</exception>
        /// <exception cref="InvalidOperationException">If no service account can be discovered
        /// from either the <see cref="AppOptions"/> or the deployment environment.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while creating a custom
        /// token.</exception>
        /// <param name="uid">The UID to store in the token. This identifies the user to other
        /// Firebase services (Realtime Database, Firebase Auth, etc.). Must not be longer than
        /// 128 characters.</param>
        /// <param name="developerClaims">Additional claims to be stored in the token, and made
        /// available to Firebase security rules. These must be serializable to JSON, and must not
        /// contain any standard JWT claims.</param>
        public async Task<string> CreateCustomTokenAsync(
            string uid, IDictionary<string, object> developerClaims)
        {
            return await this.CreateCustomTokenAsync(uid, developerClaims, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Firebase custom token for the given user ID containing the specified
        /// additional claims. This token can then be sent back to a client application to be used
        /// with the
        /// <a href="https://firebase.google.com/docs/auth/admin/create-custom-tokens#sign_in_using_custom_tokens_on_clients">
        /// signInWithCustomToken</a> authentication API.
        /// <para>This method uses the same mechanisms as
        /// <see cref="CreateCustomTokenAsync(string)"/> to sign custom tokens.</para>
        /// </summary>
        /// <returns>A task that completes with a Firebase custom token.</returns>
        /// <exception cref="ArgumentException">If <paramref name="uid"/> is null, empty or longer
        /// than 128 characters. Or, if <paramref name="developerClaims"/> contains any standard
        /// JWT claims.</exception>
        /// <exception cref="InvalidOperationException">If no service account can be discovered
        /// from either the <see cref="AppOptions"/> or the deployment environment.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while creating a custom
        /// token.</exception>
        /// <param name="uid">The UID to store in the token. This identifies the user to other
        /// Firebase services (Realtime Database, Firebase Auth, etc.). Must not be longer than
        /// 128 characters.</param>
        /// <param name="developerClaims">Additional claims to be stored in the token, and made
        /// available to Firebase security rules. These must be serializable to JSON, and must not
        /// contain any standard JWT claims.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        public async Task<string> CreateCustomTokenAsync(
            string uid,
            IDictionary<string, object> developerClaims,
            CancellationToken cancellationToken)
        {
            var tokenFactory = this.IfNotDeleted(() => this.tokenFactory.Value);
            return await tokenFactory.CreateCustomTokenAsync(
                uid, developerClaims, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Parses and verifies a Firebase ID token.
        /// <para>A Firebase client app can identify itself to a trusted back-end server by sending
        /// its Firebase ID Token (accessible via the <c>getIdToken()</c> API in the Firebase
        /// client SDK) with its requests. The back-end server can then use this method
        /// to verify that the token is valid. This method ensures that the token is correctly
        /// signed, has not expired, and it was issued against the Firebase project associated with
        /// this <c>FirebaseAuth</c> instance.</para>
        /// <para>See <a href="https://firebase.google.com/docs/auth/admin/verify-id-tokens">Verify
        /// ID Tokens</a> for code samples and detailed documentation.</para>
        /// </summary>
        /// <returns>A task that completes with a <see cref="FirebaseToken"/> representing
        /// the verified and decoded ID token.</returns>
        /// <exception cref="ArgumentException">If ID token argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If the ID token fails to verify.</exception>
        /// <param name="idToken">A Firebase ID token string to parse and verify.</param>
        public async Task<FirebaseToken> VerifyIdTokenAsync(string idToken)
        {
            return await this.VerifyIdTokenAsync(idToken, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Parses and verifies a Firebase ID token.
        /// <para>A Firebase client app can identify itself to a trusted back-end server by sending
        /// its Firebase ID Token (accessible via the <c>getIdToken()</c> API in the Firebase
        /// client SDK) with its requests. The back-end server can then use this method
        /// to verify that the token is valid. This method ensures that the token is correctly
        /// signed, has not expired, and it was issued against the Firebase project associated with
        /// this <c>FirebaseAuth</c> instance.</para>
        /// <para>See <a href="https://firebase.google.com/docs/auth/admin/verify-id-tokens">Verify
        /// ID Tokens</a> for code samples and detailed documentation.</para>
        /// </summary>
        /// <returns>A task that completes with a <see cref="FirebaseToken"/> representing
        /// the verified and decoded ID token.</returns>
        /// <exception cref="ArgumentException">If ID token argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If the ID token fails to verify.</exception>
        /// <param name="idToken">A Firebase ID token string to parse and verify.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        public async Task<FirebaseToken> VerifyIdTokenAsync(
            string idToken, CancellationToken cancellationToken)
        {
            var idTokenVerifier = this.IfNotDeleted(() => this.idTokenVerifier.Value);
            return await idTokenVerifier.VerifyTokenAsync(idToken, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new user account with the attributes contained in the specified <see cref="UserRecordArgs"/>.
        /// </summary>
        /// <param name="args">Attributes to add to the new user account.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// the newly created user account.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="args"/> is null.</exception>
        /// <exception cref="ArgumentException">If any of the values in <paramref name="args"/> are invalid.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while creating the user account.</exception>
        public async Task<UserRecord> CreateUserAsync(UserRecordArgs args)
        {
            return await this.CreateUserAsync(args, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new user account with the attributes contained in the specified <see cref="UserRecordArgs"/>.
        /// </summary>
        /// <param name="args">Attributes to add to the new user account.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// the newly created user account.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="args"/> is null.</exception>
        /// <exception cref="ArgumentException">If any of the values in <paramref name="args"/> are invalid.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while creating the user account.</exception>
        public async Task<UserRecord> CreateUserAsync(
            UserRecordArgs args, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);
            var uid = await userManager.CreateUserAsync(args, cancellationToken)
                .ConfigureAwait(false);
            return await userManager.GetUserByIdAsync(uid, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="UserRecord"/> object containing information about the user who's
        /// user ID was specified in <paramref name="uid"/>.
        /// </summary>
        /// <param name="uid">The user ID for the user who's data is to be retrieved.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// a user with the specified user ID.</returns>
        /// <exception cref="ArgumentException">If user ID argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If a user cannot be found with the specified user ID.</exception>
        public async Task<UserRecord> GetUserAsync(string uid)
        {
            return await this.GetUserAsync(uid, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="UserRecord"/> object containing information about the user who's
        /// user ID was specified in <paramref name="uid"/>.
        /// </summary>
        /// <param name="uid">The user ID for the user who's data is to be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// a user with the specified user ID.</returns>
        /// <exception cref="ArgumentException">If user ID argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If a user cannot be found with the specified user ID.</exception>
        public async Task<UserRecord> GetUserAsync(
            string uid, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            return await userManager.GetUserByIdAsync(uid, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="UserRecord"/> object containing information about the user identified by
        /// <paramref name="email"/>.
        /// </summary>
        /// <param name="email">The email of the user who's data is to be retrieved.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// a user with the specified email.</returns>
        /// <exception cref="ArgumentException">If the email argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If a user cannot be found with the specified email.</exception>
        public async Task<UserRecord> GetUserByEmailAsync(string email)
        {
            return await this.GetUserByEmailAsync(email, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="UserRecord"/> object containing information about the user identified by
        /// <paramref name="email"/>.
        /// </summary>
        /// <param name="email">The email of the user who's data is to be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// a user with the specified email.</returns>
        /// <exception cref="ArgumentException">If the email argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If a user cannot be found with the specified email.</exception>
        public async Task<UserRecord> GetUserByEmailAsync(
            string email, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            return await userManager.GetUserByEmailAsync(email, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="UserRecord"/> object containing information about the user identified by
        /// <paramref name="phoneNumber"/>.
        /// </summary>
        /// <param name="phoneNumber">The phone number of the user who's data is to be retrieved.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// a user with the specified phone number.</returns>
        /// <exception cref="ArgumentException">If the phone number argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If a user cannot be found with the specified phone number.</exception>
        public async Task<UserRecord> GetUserByPhoneNumberAsync(string phoneNumber)
        {
            return await this.GetUserByPhoneNumberAsync(phoneNumber, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="UserRecord"/> object containing information about the user identified by
        /// <paramref name="phoneNumber"/>.
        /// </summary>
        /// <param name="phoneNumber">The phone number of the user who's data is to be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// a user with the specified phone number.</returns>
        /// <exception cref="ArgumentException">If the phone number argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If a user cannot be found with the specified phone number.</exception>
        public async Task<UserRecord> GetUserByPhoneNumberAsync(
            string phoneNumber, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            return await userManager.GetUserByPhoneNumberAsync(phoneNumber, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the user data corresponding to the specified identifiers.
        /// <para>
        /// There are no ordering guarantees; in particular, the nth entry in the users result list
        /// is not guaranteed to correspond to the nth entry in the input parameters list.
        /// </para>
        /// <para>
        /// A maximum of 100 identifiers may be supplied. If more than 100 identifiers are specified,
        /// this method throws an <c>ArgumentException</c>.
        /// </para>
        /// </summary>
        /// <param name="identifiers">The identifiers used to indicate which user records should be
        /// returned. Must have 100 entries or fewer.</param>
        /// <returns>A {@code Task} that resolves to the corresponding user records.</returns>
        /// <exception cref="ArgumentException">If any of the identifiers are invalid or if more
        /// than 100 identifiers are specified.</exception>
        public async Task<GetUsersResult> GetUsersAsync(
            IReadOnlyCollection<UserIdentifier> identifiers)
        {
            return await this.GetUsersAsync(identifiers, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the user data corresponding to the specified identifiers.
        /// <para>
        /// There are no ordering guarantees; in particular, the nth entry in the users result list
        /// is not guaranteed to correspond to the nth entry in the input parameters list.
        /// </para>
        /// <para>
        /// A maximum of 100 identifiers may be supplied. If more than 100 identifiers are specified,
        /// this method throws an <c>ArgumentException</c>.
        /// </para>
        /// </summary>
        /// <param name="identifiers">The identifiers used to indicate which user records should be
        /// returned. Must have 100 entries or fewer.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A {@code Task} that resolves to the corresponding user records.</returns>
        /// <exception cref="ArgumentException">If any of the identifiers are invalid or if more
        /// than 100 identifiers are specified.</exception>
        public async Task<GetUsersResult> GetUsersAsync(
            IReadOnlyCollection<UserIdentifier> identifiers, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            GetAccountInfoResponse response = await userManager.GetAccountInfoByIdentifiersAsync(identifiers, cancellationToken)
                .ConfigureAwait(false);

            IEnumerable<UserRecord> userRecords;
            if (response.Users != null)
            {
                userRecords = response.Users.Select(user => new UserRecord(user));
            }
            else
            {
                userRecords = new List<UserRecord>();
            }

            IList<UserIdentifier> notFound = new List<UserIdentifier>();
            foreach (var id in identifiers)
            {
                if (!this.IsUserFound(id, userRecords))
                {
                    notFound.Add(id);
                }
            }

            return new GetUsersResult { Users = userRecords, NotFound = notFound };
        }

        /// <summary>
        /// Updates an existing user account with the attributes contained in the specified <see cref="UserRecordArgs"/>.
        /// The <see cref="UserRecordArgs.Uid"/> property must be specified.
        /// </summary>
        /// <param name="args">The attributes to update.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// the updated user account.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="args"/> is null.</exception>
        /// <exception cref="ArgumentException">If any of the values in <paramref name="args"/> are invalid.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while updating the user account.</exception>
        public async Task<UserRecord> UpdateUserAsync(UserRecordArgs args)
        {
            return await this.UpdateUserAsync(args, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Updates an existing user account with the attributes contained in the specified <see cref="UserRecordArgs"/>.
        /// The <see cref="UserRecordArgs.Uid"/> property must be specified.
        /// </summary>
        /// <param name="args">The attributes to update.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A task that completes with a <see cref="UserRecord"/> representing
        /// the updated user account.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="args"/> is null.</exception>
        /// <exception cref="ArgumentException">If any of the values in <paramref name="args"/> are invalid.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while updating the user account.</exception>
        public async Task<UserRecord> UpdateUserAsync(
            UserRecordArgs args, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);
            var uid = await userManager.UpdateUserAsync(args, cancellationToken)
                .ConfigureAwait(false);
            return await userManager.GetUserByIdAsync(uid, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the user identified by the specified <paramref name="uid"/>.
        /// </summary>
        /// <param name="uid">A user ID string.</param>
        /// <returns>A task that completes when the user account has been deleted.</returns>
        /// <exception cref="ArgumentException">If the user ID argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while deleting the user.</exception>
        public async Task DeleteUserAsync(string uid)
        {
            await this.DeleteUserAsync(uid, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the user identified by the specified <paramref name="uid"/>.
        /// </summary>
        /// <param name="uid">A user ID string.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A task that completes when the user account has been deleted.</returns>
        /// <exception cref="ArgumentException">If the user ID argument is null or empty.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while deleting the user.</exception>
        public async Task DeleteUserAsync(string uid, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            await userManager.DeleteUserAsync(uid, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the users specified by the given identifiers.
        /// <para>
        /// Deleting a non-existing user won't generate an error. (i.e. this method is idempotent.)
        /// Non-existing users will be considered to be successfully deleted, and will therefore be
        /// counted in the DeleteUserResult.SuccessCount value.
        /// </para>
        /// <para>
        /// A maximum of 1000 identifiers may be supplied. If more than 1000 identifiers are
        /// specified, this method throws an <c>ArgumentException</c>.
        /// </para>
        /// <para>
        /// This API is currently rate limited at the server to 1 QPS. If you exceed this, you may
        /// get a quota exceeded error. Therefore, if you want to delete more than 1000 users, you
        /// may need to add a delay to ensure you don't go over this limit.
        /// </para>
        /// </summary>
        /// <param name="uids">The uids of the users to be deleted. Must have 1000 or fewer entries.
        /// </param>
        /// <returns>A {@code Task} that resolves to the total number of successful/failed
        /// deletions, as well as the array of errors that correspond to the failed deletions.
        /// </returns>
        /// <exception cref="ArgumentException">If any of the identifiers are invalid or if more
        /// than 1000 identifiers are specified.</exception>
        public async Task<DeleteUsersResult> DeleteUsersAsync(IReadOnlyList<string> uids)
        {
            return await this.DeleteUsersAsync(uids, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the users specified by the given identifiers.
        /// <para>
        /// Deleting a non-existing user won't generate an error. (i.e. this method is idempotent.)
        /// Non-existing users will be considered to be successfully deleted, and will therefore be
        /// counted in the DeleteUserResult.SuccessCount value.
        /// </para>
        /// <para>
        /// A maximum of 1000 identifiers may be supplied. If more than 1000 identifiers are
        /// specified, this method throws an <c>ArgumentException</c>.
        /// </para>
        /// <para>
        /// This API is currently rate limited at the server to 1 QPS. If you exceed this, you may
        /// get a quota exceeded error. Therefore, if you want to delete more than 1000 users, you
        /// may need to add a delay to ensure you don't go over this limit.
        /// </para>
        /// </summary>
        /// <param name="uids">The uids of the users to be deleted. Must have 1000 or fewer entries.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        /// <returns>A {@code Task} that resolves to the total number of successful/failed
        /// deletions, as well as the array of errors that correspond to the failed deletions.
        /// </returns>
        /// <exception cref="ArgumentException">If any of the identifiers are invalid or if more
        /// than 1000 identifiers are specified.</exception>
        public async Task<DeleteUsersResult> DeleteUsersAsync(IReadOnlyList<string> uids, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            return await userManager.DeleteUsersAsync(uids, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the specified custom claims on an existing user account. A null claims value
        /// removes any claims currently set on the user account. The claims must serialize into
        /// a valid JSON string. The serialized claims must not be larger than 1000 characters.
        /// </summary>
        /// <returns>A task that completes when the claims have been set.</returns>
        /// <exception cref="ArgumentException">If <paramref name="uid"/> is null, empty or longer
        /// than 128 characters. Or, if the serialized <paramref name="claims"/> is larger than 1000
        /// characters.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while setting custom claims.</exception>
        /// <param name="uid">The user ID string for the custom claims will be set. Must not be null
        /// or longer than 128 characters.
        /// </param>
        /// <param name="claims">The claims to be stored on the user account, and made
        /// available to Firebase security rules. These must be serializable to JSON, and the
        /// serialized claims should not be larger than 1000 characters.</param>
        public async Task SetCustomUserClaimsAsync(
            string uid, IReadOnlyDictionary<string, object> claims)
        {
            await this.SetCustomUserClaimsAsync(uid, claims, default(CancellationToken))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the specified custom claims on an existing user account. A null claims value
        /// removes any claims currently set on the user account. The claims should serialize into
        /// a valid JSON string. The serialized claims must not be larger than 1000 characters.
        /// </summary>
        /// <returns>A task that completes when the claims have been set.</returns>
        /// <exception cref="ArgumentException">If <paramref name="uid"/> is null, empty or longer
        /// than 128 characters. Or, if the serialized <paramref name="claims"/> is larger than 1000
        /// characters.</exception>
        /// <exception cref="FirebaseAuthException">If an error occurs while setting custom claims.</exception>
        /// <param name="uid">The user ID string for the custom claims will be set. Must not be null
        /// or longer than 128 characters.
        /// </param>
        /// <param name="claims">The claims to be stored on the user account, and made
        /// available to Firebase security rules. These must be serializable to JSON, and after
        /// serialization it should not be larger than 1000 characters.</param>
        /// <param name="cancellationToken">A cancellation token to monitor the asynchronous
        /// operation.</param>
        public async Task SetCustomUserClaimsAsync(
            string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);
            var user = new UserRecordArgs()
            {
                Uid = uid,
                CustomClaims = claims,
            };

            await userManager.UpdateUserAsync(user, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets an async enumerable to iterate or page through users starting from the specified
        /// page token. If the page token is null or unspecified, iteration starts from the first
        /// page. See <a href="https://googleapis.github.io/google-cloud-dotnet/docs/guides/page-streaming.html">
        /// Page Streaming</a> for more details on how to use this API.
        /// </summary>
        /// <param name="options">The options to control the starting point and page size. Pass null
        /// to list from the beginning with default settings.</param>
        /// <returns>A <see cref="PagedAsyncEnumerable{ExportedUserRecords, ExportedUserRecord}"/> instance.</returns>
        public PagedAsyncEnumerable<ExportedUserRecords, ExportedUserRecord> ListUsersAsync(
            ListUsersOptions options)
        {
            var userManager = this.IfNotDeleted(() => this.userManager.Value);

            return userManager.ListUsers(options);
        }

        /// <summary>
        /// Deletes this <see cref="FirebaseAuth"/> service instance.
        /// </summary>
        void IFirebaseService.Delete()
        {
            lock (this.authLock)
            {
                this.deleted = true;
                this.tokenFactory.DisposeIfCreated();
                this.userManager.DisposeIfCreated();
            }
        }

        private TResult IfNotDeleted<TResult>(Func<TResult> func)
        {
            lock (this.authLock)
            {
                if (this.deleted)
                {
                    throw new InvalidOperationException("Cannot invoke after deleting the app.");
                }

                return func();
            }
        }

        private bool IsUserFound(UserIdentifier id, IEnumerable<UserRecord> userRecords)
        {
            foreach (var userRecord in userRecords)
            {
                if (id.Matches(userRecord))
                {
                    return true;
                }
            }

            return false;
        }

        internal sealed class FirebaseAuthArgs
        {
            internal Lazy<FirebaseTokenFactory> TokenFactory { get; set; }

            internal Lazy<FirebaseTokenVerifier> IdTokenVerifier { get; set; }

            internal Lazy<FirebaseUserManager> UserManager { get; set; }

            internal static FirebaseAuthArgs Create(FirebaseApp app)
            {
                return new FirebaseAuthArgs()
                {
                    TokenFactory = new Lazy<FirebaseTokenFactory>(
                        () => FirebaseTokenFactory.Create(app), true),
                    IdTokenVerifier = new Lazy<FirebaseTokenVerifier>(
                        () => FirebaseTokenVerifier.CreateIDTokenVerifier(app), true),
                    UserManager = new Lazy<FirebaseUserManager>(
                        () => FirebaseUserManager.Create(app), true),
                };
            }
        }
    }
}
