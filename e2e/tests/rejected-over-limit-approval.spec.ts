import { expect, test } from '@playwright/test';
import { InvoiceApprovalPage } from '../pages/invoice-approval.page';

test('Manager sees the rejection reason and invoice stays pending above the limit', async ({ page }) => {
  const invoicePage = new InvoiceApprovalPage(page);
  const customerName = `Playwright Manager Limit ${Date.now()}`;

  await page.goto('/');
  await invoicePage.signIn('demo.manager', 'ChangeMe123!');
  await invoicePage.createAndSubmitInvoice(customerName, 50_001);
  await invoicePage.openApprovals();
  await invoicePage.approveInvoice(customerName);

  await expect(invoicePage.decisionError()).toHaveText('Manager limit is 50,000');
  await page.reload();
  await invoicePage.openInvoices();
  await expect(invoicePage.statusFor(customerName)).toHaveText('Pending Approval');
});
