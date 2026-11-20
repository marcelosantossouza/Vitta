import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastContainer } from './shared/toast-container';

@Component({
  selector: 'vt-root',
  imports: [RouterOutlet, ToastContainer],
  template: '<router-outlet /><vt-toast-container />',
})
export class App {}
