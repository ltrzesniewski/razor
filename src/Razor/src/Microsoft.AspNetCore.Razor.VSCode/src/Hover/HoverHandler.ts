/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient';
import { RazorDocumentManager } from '../RazorDocumentManager';
import { RazorLogger } from '../RazorLogger';
import { RazorLanguageServerClient } from './../RazorLanguageServerClient';
import { SerializableHoverParams } from './../RPC/SerializableHoverParams';

export class HoverHandler {
    private static readonly getHoverEndpoint = 'razor/hover';
    private hoverRequestType: RequestType<SerializableHoverParams, vscode.Hover | null, any> =
        new RequestType(HoverHandler.getHoverEndpoint);

    constructor(
        private readonly documentManager: RazorDocumentManager,
        private readonly serverClient: RazorLanguageServerClient,
        private readonly logger: RazorLogger) {
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<SerializableHoverParams, vscode.Hover | null, any>(
            this.hoverRequestType,
            async (request: SerializableHoverParams, token: vscode.CancellationToken) => this.getHover(request, token));
    }

    private async getHover(
        hoverParams: SerializableHoverParams,
        cancellationToken: vscode.CancellationToken): Promise<vscode.Hover | null> {
            const razorDocumentUri = vscode.Uri.parse(hoverParams.hostDocument.uri);
            const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
            if (razorDocument === undefined) {
                return null;
            }

            const virtualCSharpUri = razorDocument.csharpDocument.uri;

            const results = await vscode.commands.executeCommand<vscode.Hover[]>(
                'vscode.executeHoverProvider',
                virtualCSharpUri,
                hoverParams.projectedPosition);

            if (!results || results.length === 0) {
                this.logger.logMessage('No hover information available.');
                return null;
            }

            // At the vscode.HoverProvider layer we can only return a single hover result. Because of this limitation we need to
            // be smart about combining multiple hovers content or only take a single hover result. For now we'll only take one
            // of them and then based on user feedback we can change this approach in the future.
            const applicableHover = results.filter(item => item.range)[0];
            if (!applicableHover) {
                // No hovers available with a range.
                return null;
            }

            const rewrittenContent = new Array<vscode.MarkdownString>();
            for (const content of applicableHover.contents) {
                // For some reason VSCode doesn't respect the hover contents as-is. Because of this we need to look at each permutation
                // of "content" (MarkdownString | string | { language: string; value: string }) and re-compute it as a MarkdownString or
                // string.

                if (typeof content === 'string') {
                    const markdownString = new vscode.MarkdownString(content);
                    rewrittenContent.push(markdownString);
                } else if ((content as { language: string; value: string }).language) {
                    const contentObject = (content as { language: string; value: string });
                    const markdownString = new vscode.MarkdownString();
                    markdownString.appendCodeblock(contentObject.value, contentObject.language);
                    rewrittenContent.push(markdownString);
                } else {
                    const contentValue = (content as vscode.MarkdownString).value;
                    const markdownString = new vscode.MarkdownString(contentValue);
                    rewrittenContent.push(markdownString);
                }
            }

            const hover = new vscode.Hover(rewrittenContent, applicableHover.range);
            return hover;
    }
}
