/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../RazorDocumentManager';
import { RazorLogger } from '../RazorLogger';
import { RazorLanguageServerClient } from './../RazorLanguageServerClient';
import { SerializableDocumentHighlightParams } from './../RPC/SerializableDocumentHighlightParams';

export class DocumentHighlightHandler {
    private static readonly getDocumentHighlightEndpoint = 'razor/documentHighlight';
    private documentHighlightRequestType: RequestType<SerializableDocumentHighlightParams, vscode.DocumentHighlight[] | null, any> =
        new RequestType(DocumentHighlightHandler.getDocumentHighlightEndpoint);

    constructor(
        private readonly documentManager: RazorDocumentManager,
        private readonly serverClient: RazorLanguageServerClient,
        private readonly logger: RazorLogger) {
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableDocumentHighlightParams, vscode.DocumentHighlight[] | null, any>(
            this.documentHighlightRequestType,
            async (request: SerializableDocumentHighlightParams, token: vscode.CancellationToken) => this.getDocumentHighlights(request, token));
    }

    private async getDocumentHighlights(
        hoverParams: SerializableDocumentHighlightParams,
        cancellationToken: vscode.CancellationToken): Promise<vscode.DocumentHighlight[] | null> {
            const razorDocumentUri = vscode.Uri.parse(hoverParams.hostDocument.uri);
            const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
            if (razorDocument === undefined) {
                return null;
            }

            const virtualCSharpUri = razorDocument.csharpDocument.uri;
            const projectedPosition = new vscode.Position(hoverParams.projectedPosition.line, hoverParams.projectedPosition.character);

            const results = await vscode.commands.executeCommand<vscode.DocumentHighlight[]>(
                'vscode.executeDocumentHighlights',
                virtualCSharpUri,
                projectedPosition) as vscode.DocumentHighlight[];

            if (!results || results.length === 0) {
                this.logger.logMessage('No highlight information available.');
                return null;
            }

            return results;
    }
}
