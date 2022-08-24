/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { SerializableFoldingRangeParams } from '../RPC/SerializableFoldingRangeParams';
import { SerializableFoldingRangeResponse } from '../RPC/SerializableFoldingRangeResponse';

export class FoldingRangeHandler {
    private static readonly provideFoldingRange = 'razor/foldingRange';
    private foldingRangeRequestType: RequestType<SerializableFoldingRangeParams, SerializableFoldingRangeResponse, any, any> = new RequestType(FoldingRangeHandler.provideFoldingRange);

    constructor(private readonly serverClient: RazorLanguageServerClient) {
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableFoldingRangeParams, SerializableFoldingRangeResponse, any, any>(
            this.foldingRangeRequestType,
            async (request, token) => this.provideFoldingRanges(request, token));
    }

    private async provideFoldingRanges(
        foldingRangeParams: SerializableFoldingRangeParams,
        cancellationToken: vscode.CancellationToken) {
        // This is currently a No-Op because we don't have a way to get folding ranges from C#/HTML.
        // Other functions accomplish this with `vscode.execute<Blank>Provider`, but that doesn't exist yet for folding ranges:
        // https://github.com/microsoft/vscode/issues/158973
        let emptyFoldingRange: vscode.FoldingRange[] = [];
        const response = new SerializableFoldingRangeResponse(emptyFoldingRange, emptyFoldingRange);
        return response;
    }
}
